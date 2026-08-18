using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// One backup of an instance, as recorded in the backup's own <c>manifest.json</c> and reported by
/// <c>kgsm instances backups &lt;instance&gt; --json</c>.
/// </summary>
/// <remarks>
/// A backup is a directory in the instance's backups store named with an opaque
/// <see cref="Id"/> (<c>&lt;instance&gt;-&lt;YYYYMMDDTHHMMSSZ&gt;-&lt;6 hex&gt;</c>); every fact
/// about it lives in the manifest, so nothing parses the id. <see cref="Sha256"/> is null for an
/// uncompressed backup — it is a directory tree, not a single artifact, so there is no one digest
/// to record (null means "not applicable", never a placeholder digest).
/// </remarks>
public record class InstanceBackup
{
    /// <summary>The backup's opaque identifier, and the name to pass to a restore.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The instance this backup was taken from.</summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    /// <summary>The blueprint the instance was created from.</summary>
    [JsonPropertyName("blueprint")]
    public string? Blueprint { get; set; }

    /// <summary>The game-server version captured in this backup; null when the engine had none recorded.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>When the backup was taken (UTC).</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Why the backup was taken — one of <see cref="BackupReason"/>. A fact fixed at capture and
    /// never edited, which is what tells a routine archive apart from one taken over a broken
    /// server.
    /// </summary>
    /// <remarks>
    /// Null means the manifest records no reason, which is <b>unknown</b> and never a guess: a
    /// backup written before the field existed cannot be identified after the fact, and inferring
    /// one from its age or position would put a classification nobody measured into the record. A
    /// surface must say so rather than showing a default.
    /// </remarks>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Whether rotation may delete this backup — one of <see cref="BackupRetention"/>. A policy, and
    /// the one part of a backup an operator revises.
    /// </summary>
    /// <remarks>
    /// Null means the manifest records no retention, which <b>is</b> prunable: that is what the
    /// field's absence means and the behaviour the backup already had. Read it through
    /// <see cref="IsPinned"/> rather than comparing the string, so null resolves the one way.
    /// </remarks>
    [JsonPropertyName("retention")]
    public string? Retention { get; set; }

    /// <summary>
    /// Whether <c>prune-backups</c> will skip this backup. Pinned backups are also not counted
    /// toward the sweep's keep window, so pinning one never erodes the rotation.
    /// </summary>
    /// <remarks>
    /// This is not a delete guard: deleting a pinned backup by id still works. Pinned means the
    /// rotation will not take it, never that an operator naming it cannot.
    /// </remarks>
    [JsonIgnore]
    public bool IsPinned => string.Equals(Retention, BackupRetention.Pinned, StringComparison.Ordinal);

    /// <summary>Whether the payload is a <c>data.tar.gz</c> archive rather than a <c>data/</c> tree.</summary>
    [JsonPropertyName("compressed")]
    public bool Compressed { get; set; }

    /// <summary>
    /// How consistent the capture is. <c>"cold"</c> means the instance was stopped for the duration,
    /// which is the only mode the engine takes today.
    /// </summary>
    [JsonPropertyName("consistency")]
    public string? Consistency { get; set; }

    /// <summary>
    /// Which of the instance's directories this backup holds (<c>install</c>, <c>saves</c>). A
    /// directory that was empty at capture time is absent — the list is what the payload actually
    /// contains, not what was asked for.
    /// </summary>
    [JsonPropertyName("sources")]
    public List<string> Sources { get; set; } = [];

    /// <summary>Size of the payload on disk, in bytes.</summary>
    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    /// <summary>Number of files captured.</summary>
    [JsonPropertyName("file_count")]
    public long FileCount { get; set; }

    /// <summary>SHA-256 of the archive, verified before a restore. Null for an uncompressed backup.</summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    /// <summary>The manifest's schema version.</summary>
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }
}
