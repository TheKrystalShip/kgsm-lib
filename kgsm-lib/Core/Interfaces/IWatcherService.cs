using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing watcher operations in KGSM.
/// Monitors game server instances to detect when they become ready for players.
/// </summary>
public interface IWatcherService
{
    /// <summary>
    /// Launches a detached background process to watch for instance readiness.
    /// Automatically selects the appropriate strategy based on instance configuration.
    /// </summary>
    /// <param name="instanceName">Instance name to start watching.</param>
    /// <returns>Result of the start watch operation.</returns>
    KgsmResult StartWatch(string instanceName);

    /// <summary>
    /// Tests the log pattern matching strategy for the specified instance.
    /// Useful for debugging startup detection patterns.
    /// </summary>
    /// <param name="instanceName">Instance name to test log pattern matching for.</param>
    /// <returns>Result of the test log watch operation.</returns>
    KgsmResult TestLogWatch(string instanceName);

    /// <summary>
    /// Tests the port monitoring strategy for the specified instance.
    /// Useful for debugging port availability detection.
    /// </summary>
    /// <param name="instanceName">Instance name to test port monitoring for.</param>
    /// <returns>Result of the test port watch operation.</returns>
    KgsmResult TestPortWatch(string instanceName);

    /// <summary>
    /// Shows watcher configuration status for the specified instance.
    /// Displays available strategies and current configuration.
    /// </summary>
    /// <param name="instanceName">Instance name to get watcher status for.</param>
    /// <returns>Result containing the watcher status information.</returns>
    KgsmResult GetStatus(string instanceName);
}
