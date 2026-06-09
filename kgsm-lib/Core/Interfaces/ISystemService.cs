using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for interacting with the KGSM system module.
/// </summary>
public interface ISystemService
{
    /// <summary>
    /// Schedules a system shutdown after the specified delay.
    /// </summary>
    /// <param name="delayMinutes">
    /// Number of minutes to wait before shutting down. Defaults to 0 (immediate).
    /// </param>
    /// <returns>Result of the shutdown command execution.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when delayMinutes is negative.</exception>
    KgsmResult Shutdown(int delayMinutes = 0);

    /// <summary>
    /// Schedules a system restart after the specified delay.
    /// </summary>
    /// <param name="delayMinutes">
    /// Number of minutes to wait before restarting. Defaults to 0 (immediate).
    /// </param>
    /// <returns>Result of the restart command execution.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when delayMinutes is negative.</exception>
    KgsmResult Restart(int delayMinutes = 0);

    /// <summary>
    /// Cancels any scheduled system shutdown or restart.
    /// </summary>
    /// <returns>Result of the cancel command execution.</returns>
    KgsmResult CancelScheduled();

    /// <summary>
    /// Gets the current system uptime as a human-readable string.
    /// </summary>
    /// <returns>Result of the uptime command execution. Stdout contains the uptime text.</returns>
    KgsmResult GetUptime();

    /// <summary>
    /// Gets the CPU load averages for the last 1, 5, and 15 minutes.
    /// </summary>
    /// <returns>Result of the load command execution. Stdout contains the load average text.</returns>
    KgsmResult GetLoad();

    /// <summary>
    /// Gets the current memory usage including total, used, free, and available.
    /// </summary>
    /// <returns>Result of the memory command execution. Stdout contains the formatted memory text.</returns>
    KgsmResult GetMemory();

    /// <summary>
    /// Gets the disk usage for the root filesystem.
    /// </summary>
    /// <returns>Result of the disk command execution. Stdout contains the formatted disk usage text.</returns>
    KgsmResult GetDisk();

    /// <summary>
    /// Checks whether a system reboot is required.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a reboot is required; otherwise <c>false</c>.
    /// </returns>
    bool IsRebootRequired();

    /// <summary>
    /// Gets comprehensive system information as raw text.
    /// </summary>
    /// <returns>Result of the info command execution. Stdout contains the formatted system info.</returns>
    KgsmResult GetInfo();

    /// <summary>
    /// Gets comprehensive system information deserialized from JSON output.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON response into.</typeparam>
    /// <returns>
    /// The deserialized system info, or <c>null</c> if execution or deserialization fails.
    /// </returns>
    T? GetInfo<T>();
}
