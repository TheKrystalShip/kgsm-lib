using System.Text.Json.Serialization;

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
    /// Gets or sets the current version.
    /// </summary>
    [JsonPropertyName("current")]
    public string Current { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest version. Null when KGSM did not check
    /// (fast mode, or current version unknown).
    /// </summary>
    [JsonPropertyName("latest")]
    public string? Latest { get; set; }

    /// <summary>
    /// Gets or sets whether KGSM actually performed an update check.
    /// False in fast mode or when the current version is unknown.
    /// </summary>
    [JsonPropertyName("checked")]
    public bool Checked { get; set; } = false;

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
    /// Gets or sets the blueprint name.
    /// </summary>
    [JsonPropertyName("blueprint")]
    public string Blueprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime environment.
    /// </summary>
    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory.
    /// </summary>
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = string.Empty;

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
    /// Gets or sets the disk usage.
    /// </summary>
    [JsonPropertyName("disk_usage")]
    public string DiskUsage { get; set; } = string.Empty;
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
    /// Gets or sets whether the instance is running.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringToBoolConverter))]
    public bool Status { get; set; } = false;

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
