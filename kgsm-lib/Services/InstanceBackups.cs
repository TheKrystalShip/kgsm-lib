using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Native;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// The default <see cref="IInstanceBackups"/> — the jailed read authority for an instance's backups
/// store, rooted at <c>Instance.BackupsDir</c>.
/// </summary>
/// <remarks>
/// Like <see cref="InstanceFiles"/> this does direct <c>System.IO</c> rather than shelling the engine,
/// and it shares that service's containment rule through <see cref="InstanceJail"/> — the root differs,
/// the rule does not.
/// <para>
/// The manifest is the gate, not the directory listing. A directory in the backups store is only a
/// backup if it carries a readable <c>manifest.json</c>, which is exactly what makes a half-built backup
/// (still being staged) and a foreign directory invisible here — the same rule the engine's own
/// <c>_list_backups</c> applies, so the two cannot disagree about what exists.
/// </para>
/// </remarks>
public sealed class InstanceBackups : IInstanceBackups
{
    // A manifest is a small flat object; anything this size is not one, and reading it would be a way to
    // make the process allocate arbitrarily by planting a file in the backups store.
    private const long MaxManifestBytes = 256 * 1024;

    private readonly IInstanceService _instances;
    private readonly ILogger<InstanceBackups> _logger;

    /// <summary>Initializes a new instance of the <see cref="InstanceBackups"/> class.</summary>
    /// <param name="instances">Resolves an instance's <c>BackupsDir</c> — the jail root.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public InstanceBackups(IInstanceService instances, ILogger<InstanceBackups> logger)
    {
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public FileOpResult<BackupArchive> OpenArchive(string instance, string backupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instance, nameof(instance));
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId, nameof(backupId));

        if (!TryRoot(instance, out string root, out FileOpOutcome rootFailure))
            return FileOpResult<BackupArchive>.Fail(rootFailure);

        // The id is a single directory name, never a path. Rejecting a separator outright means a
        // traversal attempt is refused for what it is, instead of relying on the jail to catch the
        // result of it — the jail is the backstop here, not the only check.
        if (backupId.Contains('/') || backupId.Contains('\\') || backupId is "." or "..")
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.OutOfJail);

        if (!InstanceJail.TryResolve(root, backupId, out string backupDir, out _))
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.OutOfJail);

        if (LibC.Lstat(backupDir) != LstatKind.Directory)
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.NotFound);

        // No readable manifest ⇒ not a backup. Checked before anything is opened, so an interrupted
        // build can never be served as if it were complete.
        BackupManifest? manifest = ReadManifest(Path.Combine(backupDir, "manifest.json"));
        if (manifest is null)
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.NotFound);

        if (!manifest.Compressed)
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.NotAFile,
                "this backup is an uncompressed directory tree, not a single archive");

        string archivePath = Path.Combine(backupDir, "data.tar.gz");
        LstatKind kind = LibC.Lstat(archivePath);
        if (kind == LstatKind.Missing)
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.NotFound,
                "the backup is marked compressed but has no archive");
        if (kind != LstatKind.Regular)
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.NotAFile);

        try
        {
            var info = new FileInfo(archivePath);
            // Opened with FileShare.Read so a concurrent reader (another download, a restore verifying
            // the digest) is never locked out. Nothing rewrites a published backup in place — the engine
            // builds each one in a staging directory and renames it into the store — so there is no
            // writer to race with.
            var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);

            return FileOpResult<BackupArchive>.Ok(new BackupArchive(
                stream, "data.tar.gz", info.Length, info.LastWriteTimeUtc, manifest.Sha256));
        }
        catch (UnauthorizedAccessException ex)
        {
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.IoError, ex.Message);
        }
        catch (IOException ex)
        {
            return FileOpResult<BackupArchive>.Fail(FileOpOutcome.IoError, ex.Message);
        }
    }

    // Only the two fields this service acts on. The full manifest shape is InstanceBackup, which the
    // engine's own --json listing already delivers; re-reading all of it here would be a second parser
    // for the same file and a second thing to keep in step.
    private sealed record BackupManifest(bool Compressed, string? Sha256);

    private BackupManifest? ReadManifest(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxManifestBytes) return null;

            using FileStream fs = File.OpenRead(path);
            using JsonDocument doc = JsonDocument.Parse(fs);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            bool compressed = root.TryGetProperty("compressed", out JsonElement c)
                && c.ValueKind == JsonValueKind.True;

            // sha256 is null for an uncompressed backup, and the manifest says so explicitly — carry
            // that absence through rather than turning it into an empty string.
            string? sha = root.TryGetProperty("sha256", out JsonElement s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;

            return new BackupManifest(compressed, sha);
        }
        catch (JsonException)
        {
            return null; // unreadable manifest ⇒ not a backup, same as having none
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read backup manifest at {Path}", path);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Failed to read backup manifest at {Path}", path);
            return null;
        }
    }

    private bool TryRoot(string instance, out string root, out FileOpOutcome failure)
    {
        root = string.Empty;
        failure = FileOpOutcome.Ok;

        Instance? info;
        try { info = _instances.GetInstanceInfo(instance); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve instance info for {Instance}", instance);
            failure = FileOpOutcome.InstanceUnavailable;
            return false;
        }

        string? backupsDir = info?.BackupsDir;
        if (string.IsNullOrWhiteSpace(backupsDir))
        {
            failure = FileOpOutcome.InstanceUnavailable;
            return false;
        }

        try { root = InstanceJail.CanonicalRealPath(Path.GetFullPath(backupsDir)); }
        catch (IOException)
        {
            failure = FileOpOutcome.InstanceUnavailable; // e.g. a symlink loop in the backups dir itself
            return false;
        }
        return true;
    }
}
