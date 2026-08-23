using System.Text.Json.Serialization;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Represents process information for an instance.
/// </summary>
public record class ProcessInfo
{
    /// <summary>
    /// Gets or sets the process ID.
    /// </summary>
    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    /// <summary>
    /// Gets or sets the process status.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the process start time, as UTC. Current KGSM emits ISO-8601 UTC
    /// (<c>…Z</c>); a value with an explicit UTC <c>Z</c> or offset binds to a UTC
    /// <see cref="DateTime"/>, and any other (old local-time/asctime, empty, garbage)
    /// value degrades to <see langword="null"/> without throwing — see
    /// <see cref="JsonTolerantUtcDateTimeConverter"/>. <see langword="null"/> means no
    /// honest start time, never a fabricated one.
    /// </summary>
    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(JsonTolerantUtcDateTimeConverter))]
    public DateTime? StartTime { get; set; }
}

/// <summary>
/// Represents version information for an instance.
/// </summary>
public record class VersionInfo
{
    /// <summary>
    /// Gets or sets the current version. <see langword="null"/> when it could not be read at all —
    /// the version is written inside the instance's own directory, so an instance whose library is
    /// not mounted has no readable version rather than an empty one.
    /// </summary>
    [JsonPropertyName("current")]
    public string? Current { get; set; }

    /// <summary>
    /// Gets or sets the latest version. Null when KGSM did not check
    /// (fast mode, or current version unknown).
    /// </summary>
    [JsonPropertyName("latest")]
    public string? Latest { get; set; }

    /// <summary>
    /// Gets or sets whether a comparison against <see cref="Latest"/> was possible.
    /// False when the current version is unknown, or when nothing has ever checked
    /// this instance's upstream.
    /// </summary>
    [JsonPropertyName("checked")]
    public bool Checked { get; set; } = false;

    /// <summary>
    /// Gets or sets when <see cref="Latest"/> was fetched from upstream (UTC).
    /// <see langword="null"/> when no check has ever run. In fast mode the answer
    /// comes off the record the engine keeps beside the instance, so this is how
    /// stale it is — a surface that shows the version without the moment claims a
    /// freshness it cannot support.
    /// </summary>
    [JsonPropertyName("checked_at")]
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>
    /// Gets or sets whether updates are available. Null when not checked
    /// (<see cref="Checked"/> is false) — KGSM never fabricates a value.
    /// </summary>
    [JsonPropertyName("updates_available")]
    public bool? UpdatesAvailable { get; set; }
}

/// <summary>
/// Represents configuration information for an instance.
/// </summary>
public record class ConfigurationInfo
{
    /// <summary>
    /// Gets or sets the blueprint. Readable whether or not the instance's library is mounted — for
    /// an absent one the engine takes it from the instance registry, which is on this host.
    /// </summary>
    /// <remarks>
    /// ⚠ Two spellings on one field: a mounted instance's management script reports the file name
    /// (<c>factorio.bp.yaml</c>) and the registry reports the bare name (<c>factorio</c>). Read
    /// <see cref="Instance.Blueprint"/> for one that is always the name.
    /// </remarks>
    [JsonPropertyName("blueprint")]
    public string Blueprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime environment. <see langword="null"/> when it could not be read —
    /// it lives in the instance's config, which an unmounted library takes with it.
    /// </summary>
    [JsonPropertyName("runtime")]
    public string? Runtime { get; set; }

    /// <summary>
    /// Gets or sets the directory.
    /// </summary>
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the library the instance is placed in, in the one case a status
    /// read can answer it: an instance whose library is away, where naming the disk is most of what
    /// there is to say. Empty otherwise — a mounted instance's status comes from its own management
    /// script, which knows nothing about the host's registry. <see cref="Instance.Library"/> is the
    /// field that always carries it.
    /// </summary>
    [JsonPropertyName("library")]
    public string Library { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the registered root of that library — where the instance's files are expected
    /// to be. Present on the same terms as <see cref="Library"/>; this is the path a surface names
    /// when it tells somebody which disk to plug back in.
    /// </summary>
    [JsonPropertyName("library_dir")]
    public string LibraryDir { get; set; } = string.Empty;

    // NB: no Ports here. The status surface (`status --json`) still echoes a `ports` string from
    // the management script, but nothing read it, and the canonical structured port form lives on
    // Instance.Ports (the config/info surface). The leftover wire field is harmlessly ignored
    // (System.Text.Json skips unmapped members). If a status consumer ever needs ports, add it
    // back as List<PortMapping> AND switch the mgmt-script status template to emit the array.
}

/// <summary>
/// Represents resource information for an instance.
/// </summary>
public record class ResourceInfo
{
    /// <summary>
    /// Gets or sets the disk usage. <see langword="null"/> when nothing measured it — the figure
    /// comes from walking the instance's own directory.
    /// </summary>
    [JsonPropertyName("disk_usage")]
    public string? DiskUsage { get; set; }
}

/// <summary>
/// Represents the runtime status summary for an instance.
/// </summary>
public record class InstanceRuntimeStatus
{
    /// <summary>
    /// Gets or sets the instance name.
    /// </summary>
    [JsonPropertyName("instance_name")]
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the instance is running. <see langword="null"/> when nothing measured
    /// it — the engine emits null for an instance whose library is not mounted, because every
    /// reading it would take comes out of a directory that is not there.
    /// </summary>
    /// <remarks>
    /// ⚠ Null is not <see langword="false"/>. An unreadable instance is not a stopped one, and a
    /// surface that renders the two the same way tells an operator their server is down when what
    /// happened is that a disk came out. Render null as an explicit unknown, and read
    /// <see cref="LibraryState"/> for why.
    /// </remarks>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringToBoolConverter))]
    public bool? Status { get; set; }

    /// <summary>
    /// Gets or sets the state of the library the instance is placed in. Present on every status
    /// read, mounted or not, so a consumer can join on it; <see langword="null"/> only from an
    /// engine that predates libraries.
    /// </summary>
    [JsonPropertyName("library_state")]
    public InstanceLibraryState? LibraryState { get; set; }

    /// <summary>
    /// Gets or sets the process information.
    /// </summary>
    [JsonPropertyName("process")]
    public ProcessInfo Process { get; set; } = new();

    /// <summary>
    /// Gets or sets the version information.
    /// </summary>
    [JsonPropertyName("version")]
    public VersionInfo Version { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration information.
    /// </summary>
    [JsonPropertyName("configuration")]
    public ConfigurationInfo Configuration { get; set; } = new();

    /// <summary>
    /// Gets or sets the resource information.
    /// </summary>
    [JsonPropertyName("resources")]
    public ResourceInfo Resources { get; set; } = new();

    /// <summary>
    /// Gets or sets the backup information.
    /// </summary>
    [JsonPropertyName("backups")]
    public IReadOnlyList<string> Backups { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the recent logs — a short newline-joined tail of the
    /// instance log. KGSM emits this as a string when a log exists and as an
    /// empty array (<c>[]</c>) when it does not; <see cref="JsonRecentLogsConverter"/>
    /// normalizes both to a string (empty for the no-log case).
    /// </summary>
    [JsonPropertyName("recent_logs")]
    [JsonConverter(typeof(JsonRecentLogsConverter))]
    public string RecentLogs { get; set; } = string.Empty;

    // NOTE: a failed element in a bulk read (an instance whose management file
    // cannot answer --status) is no longer carried as nullable Error/
    // RequiresRegeneration fields on this status object. KGSM still emits the
    // wire shape {"error":…, "instance":…, "requires_regeneration":true}, but
    // KgsmBulkStatusReadingConverter maps it to a Reading<InstanceRuntimeStatus>
    // at state=Unavailable (code=RequiresRegeneration) — see Reading{T} and the
    // bulk return of IInstanceService.GetAllStatuses. This keeps "measured vs
    // could-not-read" a typed distinction instead of a masquerading default.
}
