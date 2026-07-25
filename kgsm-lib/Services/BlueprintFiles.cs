using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Native;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// The default <see cref="IBlueprintFiles"/> — the write-side authority for native-runtime blueprint
/// files. Injects <see cref="IKgsmCommandExecutor"/> (to learn the user blueprints directory from
/// <c>kgsm --paths --json</c> — see the interface remarks) and <see cref="ILogger{TCategoryName}"/>,
/// exactly the way <see cref="BlueprintService"/> resolves the read side; deliberately NOT
/// <see cref="IInstanceService"/>, since this authority has no instance to consult.
/// </summary>
/// <remarks>
/// Mirrors <see cref="InstanceFiles"/>'s pattern end to end:
/// <list type="number">
/// <item>The root — the user blueprints directory — is resolved fresh on every call (no caching) by
///   running <c>kgsm --paths --json</c> and reading <see cref="KgsmUserPaths.UserBlueprintsDir"/> off the
///   deserialized <see cref="KgsmPaths"/>, then canonicalising it via <see cref="CanonicalRealPath"/>.
///   This asks the engine for the path (rather than re-deriving the XDG rule in C#) via a stable,
///   machine-readable contract — no free-form text parsing. An engine too old to know <c>--json</c>
///   returns a non-zero exit, which deserializes to <see langword="null"/> and is reported honestly as
///   <see cref="FileOpOutcome.BlueprintsDirUnavailable"/> — never a fabricated path.</item>
/// <item>The only caller input is a single-segment <see cref="NativeBlueprintDraft.Name"/>/<c>name</c> —
///   rejected as <see cref="FileOpOutcome.OutOfJail"/> before any disk access unless it matches
///   <see cref="SafeName"/> (lowercase slug, no path separators). The resulting
///   <c>&lt;userDir&gt;/&lt;name&gt;.bp.yaml</c> path is still canonicalised and containment-checked
///   afterwards, as defense in depth.</item>
/// <item>File-type gating is via <see cref="LibC.Lstat"/>, exactly as in <see cref="InstanceFiles"/>.</item>
/// <item>Writes are atomic: a temp file in the same directory → <c>fsync</c> → rename.</item>
/// <item>The YAML is a deterministic STRING template (no reflection-based serializer — Native-AOT-safe),
///   field order matching <c>kgsm/templates/blueprint.tp</c>. Every string scalar is emitted
///   single-quoted with YAML's one required escape (doubling an embedded <c>'</c>) — this parses
///   identically to the unquoted/double-quoted styles real hand-authored blueprints use, but is safe for
///   ANY content (including <c>$instance_*</c> placeholders, colons, quotes) without a YAML library.</item>
/// </list>
/// </remarks>
public sealed class BlueprintFiles : IBlueprintFiles
{
    private const int MaxSymlinkHops = 64; // symlink-loop guard, same bound as InstanceFiles
    private const int MaxNameLength = 64;

    // Lowercase slug: starts/ends alphanumeric, `-`/`_` allowed only between alphanumerics — never a
    // path separator, `.`/`..`, whitespace, or an absolute-path leading `/`.
    private static readonly Regex SafeName = new(@"^[a-z0-9]+(?:[-_][a-z0-9]+)*$", RegexOptions.Compiled);

    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<BlueprintFiles> _logger;

    /// <summary>Initializes a new instance of the <see cref="BlueprintFiles"/> class.</summary>
    /// <param name="commandExecutor">Used to resolve the user blueprints directory via <c>kgsm --paths</c>.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public BlueprintFiles(IKgsmCommandExecutor commandExecutor, ILogger<BlueprintFiles> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public FileOpResult<FileStat> Create(NativeBlueprintDraft draft, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(draft, nameof(draft));

        if (string.IsNullOrWhiteSpace(draft.Native.ExecutableFile))
            return FileOpResult<FileStat>.Fail(FileOpOutcome.InvalidDraft, "native.executable_file is required");

        if (!IsSafeName(draft.Name))
            return FileOpResult<FileStat>.Fail(FileOpOutcome.OutOfJail, "blueprint name must be a safe lowercase slug");

        if (!TryUserBlueprintsDir(out string userDir, out FileOpOutcome dirFailure))
            return FileOpResult<FileStat>.Fail(dirFailure);

        if (!TryResolveTarget(userDir, draft.Name, out string real))
            return FileOpResult<FileStat>.Fail(FileOpOutcome.OutOfJail);

        LstatKind kind = LibC.Lstat(real);
        if (kind == LstatKind.Regular && !overwrite)
            return FileOpResult<FileStat>.Fail(FileOpOutcome.AlreadyExists);
        if (kind != LstatKind.Missing && kind != LstatKind.Regular)
            return FileOpResult<FileStat>.Fail(FileOpOutcome.IoError, "target path is not a regular file");

        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(RenderYaml(draft));

        // Atomic write: temp in the SAME dir → fsync → rename(2) — identical shape to InstanceFiles.Write.
        string dir = Path.GetDirectoryName(real)!;
        string tmp = Path.Combine(dir, "." + Path.GetFileName(real) + ".tmp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true); // fsync
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
            SizeBytes = bytes.LongLength,
            Mtime = mtime,
            Etag = Etag(bytes),
        });
    }

    /// <inheritdoc/>
    public FileOpResult Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (!IsSafeName(name))
            return FileOpResult.Fail(FileOpOutcome.OutOfJail);

        if (!TryUserBlueprintsDir(out string userDir, out FileOpOutcome dirFailure))
            return FileOpResult.Fail(dirFailure);

        if (!TryResolveTarget(userDir, name, out string real))
            return FileOpResult.Fail(FileOpOutcome.OutOfJail);

        // `real` is always `<userDir>/<name>.bp.yaml` — structurally never the system blueprints dir, so
        // there is nothing further to check to honor "never removes a system blueprint" (see interface remarks).
        LstatKind kind = LibC.Lstat(real);
        if (kind == LstatKind.Missing)
            return FileOpResult.Fail(FileOpOutcome.NotFound);
        if (kind != LstatKind.Regular)
            return FileOpResult.Fail(FileOpOutcome.IoError, "target path is not a regular file");

        try { File.Delete(real); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileOpResult.Fail(FileOpOutcome.IoError, ex.Message);
        }
        return FileOpResult.Ok();
    }

    /// <inheritdoc/>
    public FileOpResult<bool> Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (!IsSafeName(name))
            return FileOpResult<bool>.Fail(FileOpOutcome.OutOfJail);

        if (!TryUserBlueprintsDir(out string userDir, out FileOpOutcome dirFailure))
            return FileOpResult<bool>.Fail(dirFailure);

        if (!TryResolveTarget(userDir, name, out string real))
            return FileOpResult<bool>.Fail(FileOpOutcome.OutOfJail);

        return FileOpResult<bool>.Ok(LibC.Lstat(real) == LstatKind.Regular);
    }

    /// <inheritdoc/>
    public string Render(NativeBlueprintDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft, nameof(draft));
        return RenderYaml(draft);
    }

    /// <inheritdoc/>
    public FileOpResult<NativeBlueprintDraft> TryParse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml, nameof(yaml));
        return ParseYaml(yaml);
    }

    // ---- name safety -----------------------------------------------------------------------------

    private static bool IsSafeName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxNameLength && SafeName.IsMatch(name);

    // ---- jail root: learned from the engine, never re-derived ------------------------------------

    /// <summary>Resolves the user blueprints directory fresh on every call by asking the engine
    /// (<c>kgsm --paths --json</c>) rather than re-deriving the XDG rule in C# — see the type remarks.</summary>
    private bool TryUserBlueprintsDir(out string dir, out FileOpOutcome failure)
    {
        dir = string.Empty;
        failure = FileOpOutcome.Ok;

        KgsmPaths? paths;
        try { paths = _commandExecutor.ExecuteForJson<KgsmPaths>(["--paths", "--json"]); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query kgsm --paths --json for the user blueprints directory");
            failure = FileOpOutcome.BlueprintsDirUnavailable;
            return false;
        }

        string? raw = paths?.User?.UserBlueprintsDir;
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Null/empty covers a failed exec, a JSON parse failure, and an engine too old to know
            // --json (non-zero exit → default). All are honestly "unavailable", never a guessed path.
            _logger.LogWarning("kgsm --paths --json did not report a user blueprints directory");
            failure = FileOpOutcome.BlueprintsDirUnavailable;
            return false;
        }

        try { dir = CanonicalRealPath(Path.GetFullPath(raw.Trim())); }
        catch (IOException)
        {
            failure = FileOpOutcome.BlueprintsDirUnavailable; // e.g. a symlink loop in the reported dir itself
            return false;
        }

        if (!Directory.Exists(dir))
        {
            // kgsm's bootstrap creates this directory on every invocation (see core/paths.sh's
            // __init_user_directories, called unconditionally from core/bootstrap.sh) — so by the time
            // `kgsm --paths --json` has already run and returned it, it should exist. If it doesn't,
            // something is wrong on the engine side; report rather than silently mkdir-ing it from C#.
            failure = FileOpOutcome.BlueprintsDirUnavailable;
            return false;
        }

        return true;
    }

    // ---- the jail (defense in depth over the name-safety check above) ----------------------------

    /// <summary>Joins <paramref name="name"/> onto <paramref name="userDir"/> as
    /// <c>&lt;name&gt;.bp.yaml</c>, canonicalises the result, and confirms it stays within
    /// (the real) <paramref name="userDir"/>. <see langword="false"/> ⇒ escaped the jail (or
    /// unresolvable, e.g. a symlink loop) and must be refused.</summary>
    private static bool TryResolveTarget(string userDir, string name, out string real)
    {
        real = "";

        string lexical = Path.GetFullPath(Path.Combine(userDir, name + ".bp.yaml"));
        string resolved;
        try { resolved = CanonicalRealPath(lexical); }
        catch (IOException) { return false; } // symlink loop or similar — refuse rather than hang/throw

        bool contained = string.Equals(resolved, userDir, StringComparison.Ordinal)
            || resolved.StartsWith(userDir + "/", StringComparison.Ordinal);
        if (!contained) return false;

        real = resolved;
        return true;
    }

    /// <summary>Canonical real path (POSIX <c>realpath</c>) — identical algorithm to
    /// <see cref="InstanceFiles"/>'s helper of the same name: resolves <c>.</c>, <c>..</c> AND symlinks at
    /// EVERY path component, following symlink chains up to <see cref="MaxSymlinkHops"/>. A non-existent
    /// tail component is accepted verbatim (so a create target's not-yet-created name resolves).
    /// <paramref name="absolutePath"/> must be rooted.</summary>
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

            string[] parts = link.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (Path.IsPathRooted(link)) resolved.Clear();
            for (int i = parts.Length - 1; i >= 0; i--) todo.AddFirst(parts[i]);
        }

        return "/" + string.Join('/', resolved);
    }

    private static string Etag(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    // ---- YAML templating (deterministic string building — no reflection-based serializer) --------

    /// <summary>Templates <paramref name="draft"/> into a valid native <c>&lt;name&gt;.bp.yaml</c> string,
    /// field order matching <c>kgsm/templates/blueprint.tp</c>. Every string scalar is single-quoted
    /// (YAML's one escape: double an embedded <c>'</c>) so arbitrary content — <c>$instance_*</c>
    /// placeholders, colons, quotes — is always safe without a YAML library; nullable fields render the
    /// literal <c>null</c>, never a fabricated placeholder.</summary>
    private static string RenderYaml(NativeBlueprintDraft draft)
    {
        NativeBlueprintMetadataDraft m = draft.Metadata;
        NativeBlueprintNativeDraft n = draft.Native;

        var sb = new StringBuilder();
        sb.Append("schema_version: 1\n");
        sb.Append("name: ").Append(draft.Name).Append('\n');
        sb.Append("runtime: native\n");
        sb.Append("metadata:\n");
        sb.Append("  display_name: ").Append(YamlNullableString(m.DisplayName)).Append('\n');
        sb.Append("  description: ").Append(YamlNullableString(m.Description)).Append('\n');
        sb.Append("  rawg_slug: ").Append(YamlNullableString(m.RawgSlug)).Append('\n');
        sb.Append("  max_players: ").Append(YamlNullableInt(m.MaxPlayers)).Append('\n');
        sb.Append("  min_ram_mb: ").Append(YamlNullableInt(m.MinRamMb)).Append('\n');
        sb.Append("  recommended_ram_mb: ").Append(YamlNullableInt(m.RecommendedRamMb)).Append('\n');
        sb.Append("  base_disk_mb: ").Append(YamlNullableInt(m.BaseDiskMb)).Append('\n');

        // Top-level, OPTIONAL — omitted entirely (not even as `null`) when unset, matching how real
        // blueprints without player-presence detection simply lack these lines (e.g. factorio.bp.yaml).
        if (!string.IsNullOrEmpty(draft.PlayerJoinedRegex))
            sb.Append("player_joined_regex: ").Append(YamlQuoted(draft.PlayerJoinedRegex)).Append('\n');
        if (!string.IsNullOrEmpty(draft.PlayerLeftRegex))
            sb.Append("player_left_regex: ").Append(YamlQuoted(draft.PlayerLeftRegex)).Append('\n');

        sb.Append("native:\n");
        sb.Append("  ports: ").Append(YamlQuoted(n.Ports)).Append('\n');
        sb.Append("  steam_app_id: ").Append(n.SteamAppId.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("  client_steam_app_id: ").Append(n.ClientSteamAppId.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("  steamcmd_arguments: ").Append(YamlQuoted(n.SteamcmdArguments)).Append('\n');
        sb.Append("  is_steam_account_required: ").Append(n.IsSteamAccountRequired ? "true" : "false").Append('\n');
        sb.Append("  platform: ").Append(YamlQuoted(n.Platform)).Append('\n');
        sb.Append("  level_name: ").Append(YamlQuoted(n.LevelName)).Append('\n');
        sb.Append("  executable_subdirectory: ").Append(YamlQuoted(n.ExecutableSubdirectory)).Append('\n');
        sb.Append("  executable_file: ").Append(YamlQuoted(n.ExecutableFile)).Append('\n');
        sb.Append("  executable_arguments: ").Append(YamlQuoted(n.ExecutableArguments)).Append('\n');
        sb.Append("  stop_command: ").Append(YamlQuoted(n.StopCommand)).Append('\n');
        sb.Append("  save_command: ").Append(YamlQuoted(n.SaveCommand)).Append('\n');
        sb.Append("  startup_success_regex: ").Append(YamlQuoted(n.StartupSuccessRegex)).Append('\n');

        return sb.ToString();
    }

    private static string YamlNullableString(string? value) => value is null ? "null" : YamlQuoted(value);

    private static string YamlNullableInt(int? value) =>
        value is null ? "null" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string YamlQuoted(string value) => "'" + value.Replace("'", "''") + "'";

    // ---- YAML parsing (the exact inverse of RenderYaml — deterministic, no reflection-based parser) --

    // A `key: value` line. The key is a bare identifier anchored at the (trimmed) line start, so the first
    // colon after it always separates key from value — a colon INSIDE a quoted value (e.g. a port range
    // "2456:2458/tcp") never confuses the split.
    private static readonly Regex KeyLine = new(@"^([A-Za-z0-9_]+)\s*:\s?(.*)$", RegexOptions.Compiled);

    /// <summary>Parses native blueprint YAML back into a <see cref="NativeBlueprintDraft"/>. The flat
    /// two-level schema (top-level keys + a <c>metadata:</c> block + a <c>native:</c> block) is walked line
    /// by line, tracking which block each indented key belongs to; scalars are un-quoted as the inverse of
    /// <see cref="YamlQuoted"/>/<see cref="YamlNullableString"/>. Structural checks only — see
    /// <see cref="IBlueprintFiles.TryParse"/>.</summary>
    private static FileOpResult<NativeBlueprintDraft> ParseYaml(string yaml)
    {
        var top = new Dictionary<string, string?>(StringComparer.Ordinal);
        var meta = new Dictionary<string, string?>(StringComparer.Ordinal);
        var nat = new Dictionary<string, string?>(StringComparer.Ordinal);

        string? section = null; // null = top-level, "metadata", or "native"
        foreach (string raw in yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith('#'))
                continue;

            bool indented = raw[0] is ' ' or '\t';
            Match m = KeyLine.Match(raw.Trim());
            if (!m.Success)
                continue; // not a key line (a stray list item, say) — ignore

            string key = m.Groups[1].Value;
            string value = m.Groups[2].Value.TrimEnd();

            // A non-indented header with no value opens a block; a recognised one switches section, an
            // unrecognised one drops back to top so its (indented) children are ignored.
            if (!indented && value.Length == 0)
            {
                section = key switch { "metadata" => "metadata", "native" => "native", _ => null };
                continue;
            }

            if (!indented)
            {
                section = null; // a fresh top-level key ends any open block
                top[key] = value;
            }
            else
            {
                (section == "metadata" ? meta : section == "native" ? nat : top)[key] = value;
            }
        }

        // Structural gates (mirroring Create): a non-native runtime is out of scope; name + executable_file
        // are the two required identity fields.
        string? runtime = Unquote(top.GetValueOrDefault("runtime"));
        if (!string.IsNullOrEmpty(runtime) && !string.Equals(runtime, "native", StringComparison.OrdinalIgnoreCase))
            return FileOpResult<NativeBlueprintDraft>.Fail(FileOpOutcome.InvalidDraft,
                $"only native blueprints are supported here (runtime was '{runtime}')");

        string name = Unquote(top.GetValueOrDefault("name")) ?? string.Empty;
        if (!IsSafeName(name))
            return FileOpResult<NativeBlueprintDraft>.Fail(FileOpOutcome.InvalidDraft,
                "name must be a safe lowercase slug ([a-z0-9_-], 1–64 chars)");

        string executableFile = Str(nat, "executable_file");
        if (string.IsNullOrWhiteSpace(executableFile))
            return FileOpResult<NativeBlueprintDraft>.Fail(FileOpOutcome.InvalidDraft,
                "native.executable_file is required");

        var draft = new NativeBlueprintDraft
        {
            Name = name,
            PlayerJoinedRegex = Unquote(top.GetValueOrDefault("player_joined_regex")),
            PlayerLeftRegex = Unquote(top.GetValueOrDefault("player_left_regex")),
            Metadata = new NativeBlueprintMetadataDraft
            {
                DisplayName = Unquote(meta.GetValueOrDefault("display_name")),
                Description = Unquote(meta.GetValueOrDefault("description")),
                RawgSlug = Unquote(meta.GetValueOrDefault("rawg_slug")),
                MaxPlayers = Int(meta, "max_players"),
                MinRamMb = Int(meta, "min_ram_mb"),
                RecommendedRamMb = Int(meta, "recommended_ram_mb"),
                BaseDiskMb = Int(meta, "base_disk_mb"),
            },
            Native = new NativeBlueprintNativeDraft
            {
                Ports = Str(nat, "ports"),
                SteamAppId = Int(nat, "steam_app_id") ?? 0,
                ClientSteamAppId = Int(nat, "client_steam_app_id") ?? 0,
                SteamcmdArguments = Str(nat, "steamcmd_arguments"),
                IsSteamAccountRequired = string.Equals(Str(nat, "is_steam_account_required"), "true", StringComparison.OrdinalIgnoreCase),
                Platform = nat.ContainsKey("platform") ? Str(nat, "platform") : "linux",
                LevelName = nat.ContainsKey("level_name") ? Str(nat, "level_name") : "default",
                ExecutableSubdirectory = Str(nat, "executable_subdirectory"),
                ExecutableFile = executableFile,
                ExecutableArguments = Str(nat, "executable_arguments"),
                StopCommand = Str(nat, "stop_command"),
                SaveCommand = Str(nat, "save_command"),
                StartupSuccessRegex = Str(nat, "startup_success_regex"),
            },
        };

        return FileOpResult<NativeBlueprintDraft>.Ok(draft);
    }

    /// <summary>Un-quotes one scalar as the inverse of <see cref="YamlQuoted"/>: single-quoted (doubling
    /// the one escape), double-quoted (common hand-edit), a bare <c>null</c> → <see langword="null"/>, or a
    /// plain scalar returned verbatim. A missing key (<see langword="null"/> input) stays null.</summary>
    private static string? Unquote(string? value)
    {
        if (value is null) return null;
        string v = value.Trim();
        if (v.Length == 0) return string.Empty;
        if (string.Equals(v, "null", StringComparison.Ordinal)) return null;
        if (v.Length >= 2 && v[0] == '\'' && v[^1] == '\'')
            return v[1..^1].Replace("''", "'");
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"')
            return v[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return v;
    }

    /// <summary>A required-string native field: the un-quoted value, or the empty string when the key is
    /// absent or was written as <c>null</c> (these fields render empty, never as a fabricated placeholder).</summary>
    private static string Str(Dictionary<string, string?> d, string key) =>
        d.TryGetValue(key, out string? v) ? Unquote(v) ?? string.Empty : string.Empty;

    /// <summary>A nullable-int field: null when absent or <c>null</c>, else the parsed integer (an
    /// unparseable value is treated as unknown → null, never a fabricated 0).</summary>
    private static int? Int(Dictionary<string, string?> d, string key)
    {
        string? v = d.TryGetValue(key, out string? raw) ? Unquote(raw) : null;
        if (string.IsNullOrEmpty(v)) return null;
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : null;
    }
}
