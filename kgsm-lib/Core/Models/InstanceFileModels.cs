namespace TheKrystalShip.KGSM.Core.Models;

// Result + DTO types shared by kgsm-lib's direct-System.IO authorities — IInstanceFiles (the jailed
// instance-filesystem authority) AND IBlueprintFiles (the user-blueprints-dir create/remove authority).
// Unlike the wire models (Instance, Blueprint, ...) these never cross the KGSM process boundary: both
// services build them directly from System.IO/lstat, so they carry no [JsonPropertyName] and are
// deliberately NOT registered in KgsmJsonContext (the firewall-result precedent, FirewallModels.cs) —
// in-process only.

/// <summary>
/// The precise outcome of a jailed file operation — a closed set each consumer maps to its own surface
/// (kgsm-api → HTTP status, the assistant → tool-result text).
/// </summary>
public enum FileOpOutcome
{
    /// <summary>Success — the payload fields on the result are populated.</summary>
    Ok,

    /// <summary>The path does not exist.</summary>
    NotFound,

    /// <summary>The resolved real path escapes the instance's working-dir jail (a <c>..</c> climb, an
    /// absolute path, or a symlink whose target lies outside) — refused; never reveals the host path.</summary>
    OutOfJail,

    /// <summary>A read/write/delete target that is not a regular file (a directory, or a special file —
    /// FIFO/socket/device).</summary>
    NotAFile,

    /// <summary>A list/traverse target that is not a directory.</summary>
    NotADirectory,

    /// <summary>The file (existing, or the new content on a write) exceeds the caller-supplied byte cap.</summary>
    TooLarge,

    /// <summary>The file's bytes are not valid UTF-8 text (a NUL byte in the first 8&#160;KB, or an
    /// invalid sequence anywhere) — not readable/writable as text.</summary>
    Binary,

    /// <summary>Write: the caller's <c>ExpectedEtag</c> no longer matches the file on disk (it changed
    /// since it was read) — optimistic-concurrency rejection.</summary>
    EtagMismatch,

    /// <summary>Not emitted by <see cref="Interfaces.IInstanceFiles.Write"/> (which folds "exists" into
    /// either an overwrite or, for a directory target, <see cref="NotAFile"/>). Emitted by
    /// <see cref="Interfaces.IBlueprintFiles.Create"/> when a same-named blueprint already exists in the
    /// user dir and the caller did not opt into <c>overwrite</c>.</summary>
    AlreadyExists,

    /// <summary>Rename: the destination exists and <c>RenameOptions.Overwrite</c> is false.</summary>
    TargetExists,

    /// <summary>The instance is unknown to KGSM, or its <c>working_dir</c> could not be resolved — there
    /// is no jail root to operate against.</summary>
    InstanceUnavailable,

    /// <summary>A filesystem operation failed for a reason not covered above (permission denied, disk
    /// full, a non-empty directory on a files-only delete, ...).</summary>
    IoError,

    /// <summary><see cref="Interfaces.IBlueprintFiles"/>'s analogue of <see cref="InstanceUnavailable"/>:
    /// the engine-reported user blueprints directory (<c>kgsm --paths</c>'s <c>KGSM_USER_BLUEPRINTS_DIR</c>)
    /// could not be resolved — there is no jail root to operate against.</summary>
    BlueprintsDirUnavailable,

    /// <summary>A blueprint draft failed a STRUCTURAL check (e.g. a blank/missing
    /// <c>native.executable_file</c>) — refused before any write. Never a semantic/schema judgment (that
    /// stays the engine's authority via <see cref="Interfaces.IBlueprintService.GetInfo(string)"/>).</summary>
    InvalidDraft,

    /// <summary>An argument the caller supplied cannot be used — today, a search expression that does
    /// not compile. Distinct from <see cref="IoError"/>: nothing was attempted, so nothing failed.</summary>
    InvalidArgument,
}

/// <summary>
/// Result of a jailed file operation with no payload (<see cref="Interfaces.IInstanceFiles.Delete"/>).
/// <see cref="IsOk"/> is the coarse success bit; <see cref="Outcome"/> carries the precise status.
/// </summary>
/// <seealso cref="Interfaces.IInstanceFiles"/>
public sealed record FileOpResult
{
    /// <summary>The precise outcome.</summary>
    public required FileOpOutcome Outcome { get; init; }

    /// <summary>Optional human-readable detail (e.g. the caught exception message), never required for
    /// a caller to branch correctly — always branch on <see cref="Outcome"/>.</summary>
    public string? Message { get; init; }

    /// <summary>Coarse success bit — true iff <see cref="Outcome"/> is <see cref="FileOpOutcome.Ok"/>.</summary>
    public bool IsOk => Outcome == FileOpOutcome.Ok;

    /// <summary>Builds a success result.</summary>
    public static FileOpResult Ok() => new() { Outcome = FileOpOutcome.Ok };

    /// <summary>Builds a failure result carrying the precise <paramref name="outcome"/>.</summary>
    public static FileOpResult Fail(FileOpOutcome outcome, string? message = null) =>
        new() { Outcome = outcome, Message = message };
}

/// <summary>
/// Result of a jailed file operation that returns a payload on success (<see cref="Value"/> is populated
/// only when <see cref="IsOk"/>). Deliberately a dedicated result rather than <see cref="KgsmResult"/>
/// (which is shaped around a shelled process's exit code/stdout/stderr, not this in-process I/O).
/// </summary>
/// <seealso cref="Interfaces.IInstanceFiles"/>
public sealed record FileOpResult<T>
{
    /// <summary>The precise outcome.</summary>
    public required FileOpOutcome Outcome { get; init; }

    /// <summary>Optional human-readable detail — see <see cref="FileOpResult.Message"/>.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// The individual reasons behind <see cref="Message"/> when a failure has SEVERAL of them, kept
    /// structured so a surface can render them as a list instead of splitting the joined string back
    /// apart. Today the one producer is <see cref="Interfaces.IBlueprintFiles.WriteRaw"/>'s
    /// <see cref="FileOpOutcome.InvalidDraft"/>, carrying the engine validator's own error strings
    /// verbatim; every other outcome leaves this empty and says everything in <see cref="Message"/>.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>The payload — populated only when <see cref="IsOk"/>; default otherwise.</summary>
    public T? Value { get; init; }

    /// <summary>Coarse success bit — true iff <see cref="Outcome"/> is <see cref="FileOpOutcome.Ok"/>.</summary>
    public bool IsOk => Outcome == FileOpOutcome.Ok;

    /// <summary>Builds a success result carrying <paramref name="value"/>.</summary>
    public static FileOpResult<T> Ok(T value) => new() { Outcome = FileOpOutcome.Ok, Value = value };

    /// <summary>Builds a failure result carrying the precise <paramref name="outcome"/>.</summary>
    public static FileOpResult<T> Fail(FileOpOutcome outcome, string? message = null) =>
        new() { Outcome = outcome, Message = message };

    /// <summary>Builds a failure whose reasons are a list: <paramref name="errors"/> is kept intact on
    /// <see cref="Errors"/> and joined into <see cref="Message"/> so a caller that only reads the
    /// message still sees all of them.</summary>
    public static FileOpResult<T> Fail(FileOpOutcome outcome, IReadOnlyList<string> errors) =>
        new() { Outcome = outcome, Message = string.Join("; ", errors), Errors = errors };
}

/// <summary>The wire-agnostic kind of a listed directory entry, per its OWN (un-followed) <c>lstat</c>
/// type — a symlink reports <see cref="Symlink"/> regardless of what it points at.</summary>
public enum FileKind
{
    /// <summary>A regular file.</summary>
    File,

    /// <summary>A directory.</summary>
    Dir,

    /// <summary>A symbolic link (not followed for listing purposes).</summary>
    Symlink,

    /// <summary>Anything else — FIFO, socket, char/block device. Never opened.</summary>
    Special,
}

/// <summary>One entry in a <see cref="DirListing"/>. <see cref="SizeBytes"/>/<see cref="Mtime"/> are null
/// when genuinely unknowable (e.g. vanished between enumeration and stat) — never fabricated.</summary>
public sealed record FileEntry(string Name, FileKind Kind, long? SizeBytes, DateTimeOffset? Mtime);

/// <summary>Result of a directory list (one level, no recursion). <see cref="Truncated"/> is an honest
/// signal that more entries exist on disk than the caller's <c>maxEntries</c> cap returned — never a
/// silent drop.</summary>
public sealed record DirListing
{
    /// <summary>The listed entries, dirs-first then ordinal-ignore-case by name, capped at the caller's
    /// <c>maxEntries</c>.</summary>
    public IReadOnlyList<FileEntry> Entries { get; init; } = [];

    /// <summary>True when the directory holds more entries than were returned.</summary>
    public bool Truncated { get; init; }
}

/// <summary>Result of a file read — the raw UTF-8 text plus its identity for optimistic concurrency.</summary>
public sealed record FileContent
{
    /// <summary>The decoded UTF-8 text.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>The file's size in bytes on disk.</summary>
    public long SizeBytes { get; init; }

    /// <summary>The file's last-write time (UTC).</summary>
    public DateTimeOffset Mtime { get; init; }

    /// <summary>Content identity for optimistic concurrency — <c>"sha256:&lt;hex&gt;"</c> over the raw
    /// bytes. Round-trip this as <see cref="WriteOptions.ExpectedEtag"/> to guard a subsequent write.</summary>
    public string Etag { get; init; } = string.Empty;
}

/// <summary>Result of a write or rename — the new content identity, without re-reading the file.</summary>
public sealed record FileStat
{
    /// <summary>The written/moved file's size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>The file's last-write time (UTC) after the operation.</summary>
    public DateTimeOffset Mtime { get; init; }

    /// <summary>The new content identity — see <see cref="FileContent.Etag"/>.</summary>
    public string Etag { get; init; } = string.Empty;
}

/// <summary>Options for <see cref="Interfaces.IInstanceFiles.Write"/>. Every cap/toggle here is a
/// caller-supplied parameter — <c>IInstanceFiles</c> itself is policy-free on limits (the plan's
/// "policy stays with the consumer" rule: the assistant and kgsm-api pass different <see cref="MaxBytes"/>
/// and <see cref="Backup"/> defaults).</summary>
public sealed record WriteOptions
{
    /// <summary>When true and the target does not exist, create it (in an EXISTING parent directory only
    /// — never creates a deep tree). When false, a missing target is <see cref="FileOpOutcome.NotFound"/>.</summary>
    public bool AllowCreate { get; init; }

    /// <summary>Optimistic-concurrency guard: when non-null and the existing file's current etag differs,
    /// the write is refused with <see cref="FileOpOutcome.EtagMismatch"/>. Null = last-writer-wins.</summary>
    public string? ExpectedEtag { get; init; }

    /// <summary>When true and the target exists with non-empty content, copy it to a sibling
    /// <c>"&lt;file&gt;.kgsmbak"</c> (overwritten on every write) before writing the new content.</summary>
    public bool Backup { get; init; }

    /// <summary>The byte-length ceiling for the NEW content (UTF-8 encoded). No caller-independent
    /// default is safe, so this must be set deliberately by every caller.</summary>
    public long MaxBytes { get; init; }
}

/// <summary>Options for <see cref="Interfaces.IInstanceFiles.Delete"/>.</summary>
public sealed record DeleteOptions
{
    /// <summary>Files only by default. When true, an EMPTY directory may also be deleted — never
    /// recursive; a non-empty directory is refused regardless of this flag.</summary>
    public bool AllowDir { get; init; }
}

/// <summary>Options for <see cref="Interfaces.IInstanceFiles.Rename"/>.</summary>
public sealed record RenameOptions
{
    /// <summary>When true, an existing destination is replaced; when false (default), an existing
    /// destination is refused with <see cref="FileOpOutcome.TargetExists"/>.</summary>
    public bool Overwrite { get; init; }
}

/// <summary>Result of <see cref="Interfaces.IBlueprintFiles.ReadRaw"/> — a blueprint file's exact bytes
/// as text, plus where the engine resolved it and whether an override is in play. The content is the
/// file verbatim: comments, ordering, and container blueprints all survive, because nothing here goes
/// through a typed model.</summary>
public sealed record BlueprintFileContent
{
    /// <summary>The blueprint name that was read.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The decoded UTF-8 text, byte-for-byte what is on disk.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>The absolute path the engine resolved the name to.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Which directory the resolved file lives in — read from where the engine's answer landed,
    /// never inferred from the name.</summary>
    public BlueprintTier Tier { get; init; }

    /// <summary>Whether a shipped original exists for this name. When true and <see cref="Tier"/> is
    /// <see cref="BlueprintTier.User"/>, deleting the user file restores the original; when false, it is
    /// the only copy.</summary>
    public bool HasSystemOriginal { get; init; }

    /// <summary>Whether the file being read is a user copy shadowing a shipped original.</summary>
    public bool OverridesSystem => Tier == BlueprintTier.User && HasSystemOriginal;

    /// <summary>The file's size in bytes on disk.</summary>
    public long SizeBytes { get; init; }

    /// <summary>The file's last-write time (UTC).</summary>
    public DateTimeOffset Mtime { get; init; }

    /// <summary>Content identity for optimistic concurrency — <c>"sha256:&lt;hex&gt;"</c> over the raw
    /// bytes. Round-trip this as <see cref="BlueprintWriteOptions.ExpectedEtag"/> to guard a subsequent
    /// write.</summary>
    public string Etag { get; init; } = string.Empty;
}

/// <summary>Options for <see cref="Interfaces.IBlueprintFiles.WriteRaw"/>. Like <see cref="WriteOptions"/>
/// this is policy-free on limits — every cap is the caller's to set. <see cref="Actor"/>/<see cref="Origin"/>
/// are passed through to the event the engine emits, never defaulted to a fabricated principal.</summary>
public sealed record BlueprintWriteOptions
{
    /// <summary>Optimistic-concurrency guard: when non-null and the CURRENTLY RESOLVED blueprint file's
    /// etag differs, the write is refused with <see cref="FileOpOutcome.EtagMismatch"/>. It guards the
    /// file that was read — which for a first override is the SYSTEM file, not the user target that is
    /// about to be created. Null = last-writer-wins.</summary>
    public string? ExpectedEtag { get; init; }

    /// <summary>The byte-length ceiling for the new content (UTF-8 encoded). No caller-independent
    /// default is safe, so this must be set deliberately by every caller.</summary>
    public long MaxBytes { get; init; }

    /// <summary>The audit principal to stamp on the emitted event (<c>$KGSM_EVENT_ACTOR</c>). Null leaves
    /// the engine to apply its OS-user fallback.</summary>
    public string? Actor { get; init; }

    /// <summary>The surface that drove the write (<c>ui</c>/<c>assistant</c>/<c>discord</c>/<c>api</c>),
    /// stamped on the emitted event (<c>$KGSM_EVENT_ORIGIN</c>). Null emits no origin — the engine has no
    /// honest fallback for one and never fabricates a surface.</summary>
    public string? Origin { get; init; }
}

/// <summary>
/// An open read stream over a compressed backup's archive, with the facts needed to serve it honestly.
/// </summary>
/// <remarks>
/// <see cref="Content"/> is owned by the caller and must be disposed. <see cref="Sha256"/> comes from the
/// backup's own manifest — it is the digest recorded when the archive was written, so a consumer can
/// hand it to whoever receives the bytes and let them verify independently. It is never recomputed here:
/// re-hashing on every download would read the whole archive twice, and a digest computed from the same
/// bytes being served proves nothing about whether they are the bytes the backup was made from.
/// </remarks>
/// <param name="Content">The open archive stream — the caller disposes it.</param>
/// <param name="FileName">The archive's own file name (<c>data.tar.gz</c>).</param>
/// <param name="SizeBytes">The archive's size on disk.</param>
/// <param name="Mtime">When the archive was last written.</param>
/// <param name="Sha256">The manifest's recorded digest of the archive; null when it recorded none.</param>
public sealed record BackupArchive(
    Stream Content,
    string FileName,
    long SizeBytes,
    DateTimeOffset Mtime,
    string? Sha256) : IDisposable
{
    /// <summary>Closes the underlying archive stream.</summary>
    /// <remarks>
    /// Idempotent, so handing <see cref="Content"/> to something that takes ownership of it (an
    /// ASP.NET <c>FileStreamResult</c>, say) and also disposing this is safe rather than a double-free.
    /// </remarks>
    public void Dispose() => Content.Dispose();
}

/// <summary>
/// How a recursive walk is bounded. The defaults exist because an instance's working directory spans
/// three orders of magnitude across games — a few hundred entries for one, several hundred thousand
/// for a mod-heavy one — so an unbounded walk is not a usable operation on the large end.
/// </summary>
/// <param name="MaxResults">Stop collecting after this many matches.</param>
/// <param name="MaxDepth">How many directory levels below the start to descend.</param>
/// <param name="MaxEntriesScanned">Stop walking after inspecting this many entries, however few matched.</param>
/// <param name="IncludeBackups">
/// Whether to descend into directories named <c>backups</c>. False by default: an archived copy of a
/// config is not the file a question about the live server is about, and a search that returns both
/// makes the caller choose between two paths that look equally right.
/// </param>
public sealed record FindOptions(
    int MaxResults = 200,
    int MaxDepth = 16,
    int MaxEntriesScanned = 200_000,
    bool IncludeBackups = false);

/// <summary>One entry a walk matched. <see cref="Path"/> is relative to the jail root.</summary>
public sealed record FindMatch(string Path, FileKind Kind, long? SizeBytes, DateTimeOffset? Mtime);

/// <summary>
/// Result of a name walk. <see cref="Truncated"/> and <see cref="ScanLimitHit"/> are separate on
/// purpose: the first says more files matched than were returned, the second says the walk stopped
/// before it had seen everything. Collapsing them would let "I stopped looking" read as "that is all
/// there is" — which is how a caller concludes a file does not exist when it was simply never reached.
/// </summary>
public sealed record FindResult
{
    /// <summary>The matches, ordinal by relative path, capped at <see cref="FindOptions.MaxResults"/>.</summary>
    public IReadOnlyList<FindMatch> Matches { get; init; } = [];

    /// <summary>True when more entries matched than were returned.</summary>
    public bool Truncated { get; init; }

    /// <summary>True when the walk stopped on its entry budget rather than finishing the tree.</summary>
    public bool ScanLimitHit { get; init; }

    /// <summary>How many filesystem entries the walk inspected.</summary>
    public int EntriesScanned { get; init; }
}

/// <summary>
/// How a content search is bounded. Beyond <see cref="FindOptions"/>'s walk limits, a content search
/// also opens every candidate, so it caps file size and skips binaries.
/// </summary>
/// <param name="MaxHits">Stop collecting after this many matching lines (or files, when <paramref name="FilesOnly"/>).</param>
/// <param name="MaxFileBytes">Skip a candidate larger than this rather than reading it.</param>
/// <param name="MaxFilesRead">Stop after opening this many candidate files.</param>
/// <param name="IgnoreCase">Match case-insensitively.</param>
/// <param name="FilesOnly">Report each matching file once, without line detail (<c>grep -l</c>).</param>
/// <param name="Walk">The walk bounds applied while finding candidates.</param>
public sealed record FileSearchOptions(
    int MaxHits = 100,
    long MaxFileBytes = 1_000_000,
    int MaxFilesRead = 20_000,
    bool IgnoreCase = false,
    bool FilesOnly = false,
    FindOptions? Walk = null);

/// <summary>One matching line. <see cref="Path"/> is relative to the jail root; <see cref="LineNumber"/> is 1-based.</summary>
public sealed record SearchHit(string Path, int LineNumber, string Line);

/// <summary>
/// Result of a content search. Carries the same two distinct truncation signals as
/// <see cref="FindResult"/>, for the same reason, plus how many files were actually opened.
/// </summary>
public sealed record FileSearchResult
{
    /// <summary>The matching lines, in walk order, capped at <see cref="FileSearchOptions.MaxHits"/>.</summary>
    public IReadOnlyList<SearchHit> Hits { get; init; } = [];

    /// <summary>True when more lines matched than were returned.</summary>
    public bool Truncated { get; init; }

    /// <summary>True when the search stopped on a walk or file-read budget rather than finishing.</summary>
    public bool ScanLimitHit { get; init; }

    /// <summary>How many candidate files were opened and scanned.</summary>
    public int FilesRead { get; init; }
}
