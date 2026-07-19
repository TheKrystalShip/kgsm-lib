using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Native;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// The default <see cref="IInstanceFiles"/> — the single jailed filesystem authority for an instance's
/// working directory. Injects <see cref="IInstanceService"/> (to resolve the jail root) and
/// <see cref="ILogger{TCategoryName}"/> — deliberately NOT <see cref="IKgsmCommandExecutor"/>, since this
/// is the first kgsm-lib service that does direct <c>System.IO</c> rather than shelling the engine.
/// </summary>
/// <remarks>
/// The jail (ported from kgsm-api's <c>InstanceFileService</c>, the stronger of the ecosystem's two prior
/// jails — see <c>instance-filesystem-authority-plan.md</c>):
/// <list type="number">
/// <item>The root is <c>Instance.WorkingDir</c>, resolved fresh on every call (no caching — instances
///   get reinstalled) and canonicalised via <see cref="CanonicalRealPath"/>.</item>
/// <item>A candidate relative path is joined, then canonicalised the same way — resolving <c>.</c>/<c>..</c>
///   AND symlinks at EVERY path component (not just the leaf), following symlink chains up to
///   <see cref="MaxSymlinkHops"/> — and the result must equal the root or be a <c>root + "/"</c>-prefixed
///   descendant (ordinal), else <see cref="FileOpOutcome.OutOfJail"/>. A non-existent tail component is
///   accepted verbatim (so a create/rename target resolves).</item>
/// <item>File-type gating is via <see cref="LibC.Lstat"/> (kgsm-lib's first P/Invoke): only
///   <c>Regular</c> is opened for read/write, <c>Directory</c> for list, and FIFO/socket/device
///   (<c>Special</c>) is never opened.</item>
/// <item>Binary detection is an 8&#160;KB NUL scan plus a full strict-UTF-8 decode.</item>
/// <item>Writes are atomic: a temp file in the same directory → <c>fsync</c> → mode-preserve → rename —
///   never truncate-in-place.</item>
/// </list>
/// </remarks>
public sealed class InstanceFiles : IInstanceFiles
{
    private const int MaxSymlinkHops = 64;    // symlink-loop guard
    private const int BinaryScanBytes = 8192; // NUL-byte scan window

    private readonly IInstanceService _instances;
    private readonly ILogger<InstanceFiles> _logger;

    /// <summary>Initializes a new instance of the <see cref="InstanceFiles"/> class.</summary>
    /// <param name="instances">Resolves an instance's <c>WorkingDir</c> — the jail root.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public InstanceFiles(IInstanceService instances, ILogger<InstanceFiles> logger)
    {
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public FileOpResult<DirListing> List(string instance, string? subdir, int maxEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instance, nameof(instance));

        if (!TryRoot(instance, out string root, out FileOpOutcome rootFailure))
            return FileOpResult<DirListing>.Fail(rootFailure);
        if (!TryResolve(root, subdir, out string real, out _))
            return FileOpResult<DirListing>.Fail(FileOpOutcome.OutOfJail);

        LstatKind kind = LibC.Lstat(real);
        if (kind == LstatKind.Missing)
            return FileOpResult<DirListing>.Fail(FileOpOutcome.NotFound);
        if (kind != LstatKind.Directory)
            return FileOpResult<DirListing>.Fail(FileOpOutcome.NotADirectory);

        var all = new List<FileEntry>();
        IEnumerable<string> names;
        try { names = Directory.EnumerateFileSystemEntries(real); }
        catch (IOException) { return FileOpResult<DirListing>.Fail(FileOpOutcome.NotFound); }
        catch (UnauthorizedAccessException ex) { return FileOpResult<DirListing>.Fail(FileOpOutcome.IoError, ex.Message); }

        // lstat every entry (not just the cap) so dirs-first ordering — and therefore which entries
        // survive truncation — is deterministic; the cap is a render bound, not an enumeration one.
        foreach (string entryPath in names)
        {
            FileEntry? entry = DescribeEntry(entryPath);
            if (entry is not null) all.Add(entry);
        }

        all.Sort(static (a, b) =>
        {
            bool da = a.Kind == FileKind.Dir, db = b.Kind == FileKind.Dir;
            if (da != db) return da ? -1 : 1; // dirs first
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        bool truncated = all.Count > maxEntries;
        int take = Math.Max(0, maxEntries);
        IReadOnlyList<FileEntry> page = truncated ? all.GetRange(0, take) : all;
        return FileOpResult<DirListing>.Ok(new DirListing { Entries = page, Truncated = truncated });
    }

    /// <inheritdoc/>
    public FileOpResult<FileContent> Read(string instance, string relPath, long maxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instance, nameof(instance));

        if (!TryRoot(instance, out string root, out FileOpOutcome rootFailure))
            return FileOpResult<FileContent>.Fail(rootFailure);
        if (!TryResolve(root, relPath, out string real, out _))
            return FileOpResult<FileContent>.Fail(FileOpOutcome.OutOfJail);

        LstatKind kind = LibC.Lstat(real);
        if (kind == LstatKind.Missing)
            return FileOpResult<FileContent>.Fail(FileOpOutcome.NotFound);
        if (kind != LstatKind.Regular)
            return FileOpResult<FileContent>.Fail(FileOpOutcome.NotAFile);

        long size;
        try { size = new FileInfo(real).Length; }
        catch (IOException) { return FileOpResult<FileContent>.Fail(FileOpOutcome.NotFound); }
        if (size > maxBytes)
            return FileOpResult<FileContent>.Fail(FileOpOutcome.TooLarge);

        byte[] bytes;
        try { bytes = File.ReadAllBytes(real); }
        catch (IOException) { return FileOpResult<FileContent>.Fail(FileOpOutcome.NotFound); }
        catch (UnauthorizedAccessException ex) { return FileOpResult<FileContent>.Fail(FileOpOutcome.IoError, ex.Message); }

        if (LooksBinary(bytes))
            return FileOpResult<FileContent>.Fail(FileOpOutcome.Binary);

        DateTimeOffset mtime;
        try { mtime = new FileInfo(real).LastWriteTimeUtc; }
        catch (IOException) { mtime = default; }

        return FileOpResult<FileContent>.Ok(new FileContent
        {
            Content = Encoding.UTF8.GetString(bytes),
            SizeBytes = bytes.LongLength,
            Mtime = mtime,
            Etag = Etag(bytes),
        });
    }

    /// <inheritdoc/>
    public FileOpResult<FileStat> Write(string instance, string relPath, string content, WriteOptions opts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instance, nameof(instance));
        ArgumentNullException.ThrowIfNull(content, nameof(content));
        opts ??= new WriteOptions();

        if (!TryRoot(instance, out string root, out FileOpOutcome rootFailure))
            return FileOpResult<FileStat>.Fail(rootFailure);
        if (!TryResolve(root, relPath, out string real, out _))
            return FileOpResult<FileStat>.Fail(FileOpOutcome.OutOfJail);

        LstatKind kind = LibC.Lstat(real);
        byte[]? current = null;

        if (kind == LstatKind.Missing)
        {
            if (!opts.AllowCreate)
                return FileOpResult<FileStat>.Fail(FileOpOutcome.NotFound);

            // The parent must already exist and be a directory — creation never builds a deep tree.
            string? parent = Path.GetDirectoryName(real);
            if (string.IsNullOrEmpty(parent))
                return FileOpResult<FileStat>.Fail(FileOpOutcome.OutOfJail);

            LstatKind parentKind = LibC.Lstat(parent);
            if (parentKind == LstatKind.Missing)
                return FileOpResult<FileStat>.Fail(FileOpOutcome.NotFound);
            if (parentKind != LstatKind.Directory)
                return FileOpResult<FileStat>.Fail(FileOpOutcome.NotADirectory);
        }
        else if (kind != LstatKind.Regular)
        {
            return FileOpResult<FileStat>.Fail(FileOpOutcome.NotAFile); // refuse to clobber a dir/special
        }
        else
        {
            try { current = File.ReadAllBytes(real); }
            catch (IOException) { return FileOpResult<FileStat>.Fail(FileOpOutcome.NotFound); }
            catch (UnauthorizedAccessException ex) { return FileOpResult<FileStat>.Fail(FileOpOutcome.IoError, ex.Message); }

            if (LooksBinary(current))
                return FileOpResult<FileStat>.Fail(FileOpOutcome.Binary); // refuse to clobber a binary file

            if (!string.IsNullOrEmpty(opts.ExpectedEtag)
                && !string.Equals(opts.ExpectedEtag, Etag(current), StringComparison.Ordinal))
                return FileOpResult<FileStat>.Fail(FileOpOutcome.EtagMismatch);
        }

        byte[] newBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        if (newBytes.LongLength > opts.MaxBytes)
            return FileOpResult<FileStat>.Fail(FileOpOutcome.TooLarge);

        if (opts.Backup && current is { Length: > 0 })
        {
            try { File.WriteAllBytes(real + ".kgsmbak", current); }
            catch (IOException) { /* best-effort backup — never fails the write over it */ }
        }

        // Atomic write: temp in the SAME dir → fsync → preserve mode (existing target only) → rename.
        string dir = Path.GetDirectoryName(real)!;
        string tmp = Path.Combine(dir, "." + Path.GetFileName(real) + ".tmp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(newBytes, 0, newBytes.Length);
                fs.Flush(flushToDisk: true); // fsync
            }
            if (!OperatingSystem.IsWindows() && kind == LstatKind.Regular) // mode-preserve only makes sense for an overwrite
            {
                try { File.SetUnixFileMode(tmp, File.GetUnixFileMode(real)); } catch { /* mode best-effort */ }
            }
            File.Move(tmp, real, overwrite: true); // rename(2) — atomic on the same filesystem
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore cleanup failure */ }
            return FileOpResult<FileStat>.Fail(FileOpOutcome.IoError, ex.Message);
        }

        DateTimeOffset mtime;
        try { mtime = new FileInfo(real).LastWriteTimeUtc; }
        catch (IOException) { mtime = default; }

        return FileOpResult<FileStat>.Ok(new FileStat
        {
            SizeBytes = newBytes.LongLength,
            Mtime = mtime,
            Etag = Etag(newBytes),
        });
    }

    /// <inheritdoc/>
    public FileOpResult Delete(string instance, string relPath, DeleteOptions opts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instance, nameof(instance));
        opts ??= new DeleteOptions();

        if (!TryRoot(instance, out string root, out FileOpOutcome rootFailure))
            return FileOpResult.Fail(rootFailure);
        if (!TryResolve(root, relPath, out string real, out _))
            return FileOpResult.Fail(FileOpOutcome.OutOfJail);

        LstatKind kind = LibC.Lstat(real);
        if (kind == LstatKind.Missing)
            return FileOpResult.Fail(FileOpOutcome.NotFound);

        if (kind == LstatKind.Directory)
        {
            if (!opts.AllowDir)
                return FileOpResult.Fail(FileOpOutcome.NotAFile);

            try
            {
                if (Directory.EnumerateFileSystemEntries(real).Any())
                    return FileOpResult.Fail(FileOpOutcome.IoError, "directory is not empty"); // never recursive
                Directory.Delete(real);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return FileOpResult.Fail(FileOpOutcome.IoError, ex.Message);
            }
            return FileOpResult.Ok();
        }

        if (kind != LstatKind.Regular)
            return FileOpResult.Fail(FileOpOutcome.NotAFile); // symlink/special — refused

        try { File.Delete(real); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileOpResult.Fail(FileOpOutcome.IoError, ex.Message);
        }
        return FileOpResult.Ok();
    }

    /// <inheritdoc/>
    public FileOpResult<FileStat> Rename(string instance, string fromRel, string toRel, RenameOptions opts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instance, nameof(instance));
        opts ??= new RenameOptions();

        if (!TryRoot(instance, out string root, out FileOpOutcome rootFailure))
            return FileOpResult<FileStat>.Fail(rootFailure);
        if (!TryResolve(root, fromRel, out string fromReal, out _))
            return FileOpResult<FileStat>.Fail(FileOpOutcome.OutOfJail);
        if (!TryResolve(root, toRel, out string toReal, out _))
            return FileOpResult<FileStat>.Fail(FileOpOutcome.OutOfJail);

        LstatKind fromKind = LibC.Lstat(fromReal);
        if (fromKind == LstatKind.Missing)
            return FileOpResult<FileStat>.Fail(FileOpOutcome.NotFound);

        LstatKind toKind = LibC.Lstat(toReal);
        if (toKind != LstatKind.Missing && !opts.Overwrite)
            return FileOpResult<FileStat>.Fail(FileOpOutcome.TargetExists);

        try
        {
            if (fromKind == LstatKind.Directory)
                Directory.Move(fromReal, toReal);
            else
                File.Move(fromReal, toReal, overwrite: opts.Overwrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileOpResult<FileStat>.Fail(FileOpOutcome.IoError, ex.Message);
        }

        long size = 0;
        string etag = string.Empty;
        if (LibC.Lstat(toReal) == LstatKind.Regular)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(toReal);
                size = bytes.LongLength;
                etag = Etag(bytes);
            }
            catch (IOException) { /* best-effort stat */ }
        }

        DateTimeOffset mtime;
        try { mtime = new FileInfo(toReal).LastWriteTimeUtc; }
        catch (IOException) { mtime = default; }

        return FileOpResult<FileStat>.Ok(new FileStat { SizeBytes = size, Mtime = mtime, Etag = etag });
    }

    // ---- jail root -----------------------------------------------------------------------------

    /// <summary>Resolves the instance's canonicalised jail root, fresh every call (never cached).</summary>
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

        string? workingDir = info?.WorkingDir;
        if (string.IsNullOrWhiteSpace(workingDir))
        {
            failure = FileOpOutcome.InstanceUnavailable;
            return false;
        }

        try { root = CanonicalRealPath(Path.GetFullPath(workingDir)); }
        catch (IOException)
        {
            failure = FileOpOutcome.InstanceUnavailable; // e.g. a symlink loop in the working dir itself
            return false;
        }
        return true;
    }

    // ---- the jail (the load-bearing security boundary) -----------------------------------------

    /// <summary>Resolves a caller-supplied relative path inside <paramref name="root"/>, following
    /// symlinks at every component, and requires the real target to stay within (the real) <paramref
    /// name="root"/>. Returns the resolved absolute path + the normalized relative path;
    /// <see langword="false"/> ⇒ the target escapes the jail (or the input is malformed/unresolvable,
    /// e.g. a symlink loop) and must be refused.</summary>
    private static bool TryResolve(string root, string? relativePath, out string realTarget, out string normRel)
    {
        realTarget = "";
        normRel = "";

        string rel = (relativePath ?? "").Trim().Replace('\\', '/').Trim('/');
        if (rel.IndexOf('\0') >= 0) return false; // NUL byte — never a legitimate path

        string lexical = Path.GetFullPath(rel.Length == 0 ? root : Path.Combine(root, rel));
        string real;
        try { real = CanonicalRealPath(lexical); }
        catch (IOException) { return false; } // symlink loop or similar — refuse rather than hang/throw

        bool contained = string.Equals(real, root, StringComparison.Ordinal)
            || real.StartsWith(root + "/", StringComparison.Ordinal);
        if (!contained) { normRel = rel; return false; }

        realTarget = real;
        string display = Path.GetRelativePath(root, lexical);
        normRel = display is "." or "" ? "" : display.Replace('\\', '/');
        return true;
    }

    /// <summary>Canonical real path (POSIX <c>realpath</c>): resolves <c>.</c>, <c>..</c> AND symlinks at
    /// EVERY path component — following symlink chains and re-resolving their targets — so an
    /// intermediate-directory symlink (<c>working_dir/foo</c> → <c>/etc</c>, request <c>foo/passwd</c>) is
    /// caught, which a single leaf-only resolve misses. A non-existent tail component is accepted
    /// verbatim (so a create/rename target's not-yet-created name works). <paramref name="absolutePath"/>
    /// must be rooted.</summary>
    private static string CanonicalRealPath(string absolutePath)
    {
        var todo = new LinkedList<string>();
        foreach (string p in absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            todo.AddLast(p);

        var resolved = new List<string>();
        int hops = 0;
        while (todo.First is { } node)
        {
            todo.RemoveFirst();
            string comp = node.Value;
            if (comp == ".") continue;
            if (comp == "..")
            {
                if (resolved.Count > 0) resolved.RemoveAt(resolved.Count - 1);
                continue;
            }

            string current = resolved.Count == 0 ? "/" + comp : "/" + string.Join('/', resolved) + "/" + comp;
            string? link;
            try { link = new FileInfo(current).LinkTarget; }
            catch { link = null; }

            if (link is null)
            {
                resolved.Add(comp); // not a symlink (or doesn't exist) → accept verbatim
                continue;
            }

            if (++hops > MaxSymlinkHops)
                throw new IOException("symlink chain too long (possible loop)");

            // Expand: an absolute target restarts from root; a relative target is relative to the
            // link's PARENT directory (= the current `resolved`, since `comp` was not pushed). Prepend
            // the target's components to the work queue so they resolve against that parent.
            string[] parts = link.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (Path.IsPathRooted(link)) resolved.Clear();
            for (int i = parts.Length - 1; i >= 0; i--) todo.AddFirst(parts[i]);
        }

        return "/" + string.Join('/', resolved);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>Describes one directory entry from its own (un-followed) type — symlinks are listed but
    /// not resolved here (that's only for read/write/traverse targets).</summary>
    private static FileEntry? DescribeEntry(string entryPath)
    {
        string name = Path.GetFileName(entryPath);
        LstatKind kind = LibC.Lstat(entryPath);
        DateTimeOffset? mtime = TryMtime(entryPath);

        return kind switch
        {
            LstatKind.Directory => new FileEntry(name, FileKind.Dir, null, mtime),
            LstatKind.Symlink => new FileEntry(name, FileKind.Symlink, null, mtime),
            LstatKind.Special => new FileEntry(name, FileKind.Special, null, mtime),
            LstatKind.Regular => new FileEntry(name, FileKind.File, TryLength(entryPath), mtime),
            _ => null, // vanished between readdir and lstat — drop it
        };
    }

    private static DateTimeOffset? TryMtime(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return null; }
    }

    private static long? TryLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return null; }
    }

    /// <summary>Heuristic "is this binary, not text": a NUL byte in the first 8&#160;KB, or any invalid
    /// UTF-8 sequence anywhere (strict decode).</summary>
    private static bool LooksBinary(byte[] bytes)
    {
        int scan = Math.Min(bytes.Length, BinaryScanBytes);
        for (int i = 0; i < scan; i++)
            if (bytes[i] == 0) return true;
        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return false;
        }
        catch (DecoderFallbackException) { return true; }
    }

    /// <summary>Honest content identity for optimistic concurrency — <c>sha256:&lt;hex&gt;</c> over the
    /// raw bytes.</summary>
    private static string Etag(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
}
