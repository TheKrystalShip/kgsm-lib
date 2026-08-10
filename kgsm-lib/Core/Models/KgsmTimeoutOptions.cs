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
    /// version, listing, find, save, input, etc. These are quick, so the default
    /// stays tight enough to surface a hang promptly.
    /// </summary>
    public TimeSpan Default { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Asking an instance's upstream whether a newer build exists (<c>check-update</c>). This is a
    /// network call to something outside the host — a SteamCMD login, a container registry — and is
    /// the slow half of a status query, not a local read.
    ///
    /// The engine already bounds it from the inside: a container's registry probe is capped per image,
    /// and a check that gets no answer reports one rather than hanging. This ceiling therefore sits
    /// well above that inner one on purpose — if the OUTER timeout fires first it kills the process
    /// tree and reports a timeout for a check the engine was about to answer honestly, which is the
    /// same failure the lifecycle tier is shaped to avoid.
    /// </summary>
    public TimeSpan UpdateCheck { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Lifecycle verbs (start / stop / restart). These are not quick commands: a stop writes the
    /// instance's stop command and drains for up to its <c>stop_command_timeout_seconds</c> before the
    /// supervisor hard-kills, and a restart pays that plus a start. KGSM's own ceilings bound the work
    /// well under this (its control-socket calls allow 60s for a start and 120s for a stop), so the
    /// default here sits above the stop+start worst case: the INNER timeout must be the one that fires,
    /// or this one kills the caller mid-stop and reports a failure for an operation that then completes
    /// anyway.
    /// </summary>
    public TimeSpan Lifecycle { get; set; } = TimeSpan.FromMinutes(5);

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
