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
    /// Gets or sets the process start time.
    /// </summary>
    [JsonPropertyName("start_time")]
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
    /// Gets or sets the lifecycle manager.
    /// </summary>
    [JsonPropertyName("lifecycle_manager")]
    public string LifecycleManager { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory.
    /// </summary>
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ports.
    /// </summary>
    [JsonPropertyName("ports")]
    public string Ports { get; set; } = string.Empty;
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

    /// <summary>
    /// Gets or sets the error message for an instance whose status could not be
    /// read. Non-null only for a failed element in a bulk read
    /// (<c>instances list --status --json</c>), where KGSM emits
    /// <c>{"error":…, "instance":…, "requires_regeneration":true}</c> as the
    /// map value instead of a status object — so one bad instance never sinks
    /// the whole fleet read. Null on a successful read.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets whether the instance's management file must be regenerated
    /// to answer status queries. Set (true) only on a failed bulk-read element;
    /// null on a successful read. See <see cref="Error"/>.
    /// </summary>
    [JsonPropertyName("requires_regeneration")]
    public bool? RequiresRegeneration { get; set; }
}
