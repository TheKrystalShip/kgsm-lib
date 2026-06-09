namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Per-operation process timeouts for KGSM commands.
///
/// Different operations have wildly different durations: a status or info query
/// is sub-second, while installing or updating a large game server can take many
/// minutes (downloads + extraction). A single fixed timeout therefore can't fit
/// all of them — too tight kills a legitimate install mid-flight, too loose lets
/// a genuinely hung quick command linger. Each tier below is configurable, with
/// generous defaults out of the box.
///
/// A command that exceeds its timeout has its whole process tree killed and
/// returns a failure whose error text says it timed out.
/// </summary>
public class KgsmTimeoutOptions
{
    /// <summary>
    /// Fallback for any command without a more specific timeout: status, info,
    /// version, check-update, listing, find, save, input, etc. These are quick,
    /// so the default stays tight enough to surface a hang promptly.
    /// </summary>
    public TimeSpan Default { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Installing a new instance — downloads and extraction, can run for minutes.</summary>
    public TimeSpan Install { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Updating an instance — same order of magnitude as an install.</summary>
    public TimeSpan Update { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Uninstalling an instance — a bulk filesystem delete that can be slow on large servers.</summary>
    public TimeSpan Uninstall { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Creating a backup — a potentially large file copy.</summary>
    public TimeSpan Backup { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Restoring a backup — a potentially large file copy.</summary>
    public TimeSpan Restore { get; set; } = TimeSpan.FromMinutes(15);
}
