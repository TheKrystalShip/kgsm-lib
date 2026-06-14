using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Deserialized form of <c>kgsm system info --json</c>. KGSM wraps <c>df -h</c> /
/// <c>free -h</c> / <c>uptime</c>, so the disk, memory and load values arrive as
/// <em>human-readable strings</em> ("916G", "31Gi", "26%") — not byte counts. The
/// fields are modelled exactly as they appear on the wire; consumers parse the
/// numeric parts they need (e.g. the integer in <see cref="DiskInfo.UsePercent"/>).
/// Only <see cref="Disk"/> is consumed today; the rest are modelled for completeness
/// and future reuse (e.g. the host monitor / web surface).
/// </summary>
public record class SystemInfo
{
    /// <summary>Human-readable uptime string, e.g. "up 5 days, 56 minutes".</summary>
    [JsonPropertyName("uptime")]
    public string Uptime { get; set; } = string.Empty;

    /// <summary>Load averages (1/5/15 min) as strings.</summary>
    [JsonPropertyName("load")]
    public LoadInfo Load { get; set; } = new();

    /// <summary>Memory usage as human-readable strings (e.g. "31Gi").</summary>
    [JsonPropertyName("memory")]
    public SystemMemoryInfo Memory { get; set; } = new();

    /// <summary>Root-filesystem disk usage as human-readable <c>df -h</c> strings.</summary>
    [JsonPropertyName("disk")]
    public DiskInfo Disk { get; set; } = new();

    /// <summary>Host network identity (external IP, local IPs).</summary>
    [JsonPropertyName("network")]
    public SystemNetworkInfo Network { get; set; } = new();

    /// <summary>Whether the host reports a pending reboot.</summary>
    [JsonPropertyName("reboot_required")]
    public bool RebootRequired { get; set; } = false;
}

/// <summary>Load averages for the last 1, 5 and 15 minutes (strings on the wire).</summary>
public record class LoadInfo
{
    /// <summary>1-minute load average, e.g. "0.09".</summary>
    [JsonPropertyName("1min")]
    public string OneMin { get; set; } = string.Empty;

    /// <summary>5-minute load average, e.g. "0.25".</summary>
    [JsonPropertyName("5min")]
    public string FiveMin { get; set; } = string.Empty;

    /// <summary>15-minute load average, e.g. "0.25".</summary>
    [JsonPropertyName("15min")]
    public string FifteenMin { get; set; } = string.Empty;
}

/// <summary>Host memory usage as human-readable <c>free -h</c> strings.</summary>
public record class SystemMemoryInfo
{
    /// <summary>Total memory, e.g. "31Gi".</summary>
    [JsonPropertyName("total")]
    public string Total { get; set; } = string.Empty;

    /// <summary>Used memory, e.g. "12Gi".</summary>
    [JsonPropertyName("used")]
    public string Used { get; set; } = string.Empty;

    /// <summary>Free memory, e.g. "2.1Gi".</summary>
    [JsonPropertyName("free")]
    public string Free { get; set; } = string.Empty;

    /// <summary>Available memory, e.g. "18Gi".</summary>
    [JsonPropertyName("available")]
    public string Available { get; set; } = string.Empty;
}

/// <summary>
/// Root-filesystem disk usage as <c>df -h</c> human-readable strings. The
/// load-bearing field for the health check is <see cref="UsePercent"/> ("26%"):
/// parse its leading integer to threshold disk headroom.
/// </summary>
public record class DiskInfo
{
    /// <summary>Backing device, e.g. "/dev/nvme0n1p2".</summary>
    [JsonPropertyName("filesystem")]
    public string Filesystem { get; set; } = string.Empty;

    /// <summary>Total size, e.g. "916G".</summary>
    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;

    /// <summary>Used space, e.g. "221G".</summary>
    [JsonPropertyName("used")]
    public string Used { get; set; } = string.Empty;

    /// <summary>Available space, e.g. "649G".</summary>
    [JsonPropertyName("available")]
    public string Available { get; set; } = string.Empty;

    /// <summary>Used percentage, e.g. "26%".</summary>
    [JsonPropertyName("use_percent")]
    public string UsePercent { get; set; } = string.Empty;

    /// <summary>Mount point, e.g. "/".</summary>
    [JsonPropertyName("mount")]
    public string Mount { get; set; } = string.Empty;
}

/// <summary>Host network identity reported by <c>system info</c>.</summary>
public record class SystemNetworkInfo
{
    /// <summary>External/public IP as seen by the host.</summary>
    [JsonPropertyName("external_ip")]
    public string ExternalIp { get; set; } = string.Empty;

    /// <summary>Local interface IPs.</summary>
    [JsonPropertyName("local_ips")]
    public IReadOnlyList<string> LocalIps { get; set; } = new List<string>();
}
