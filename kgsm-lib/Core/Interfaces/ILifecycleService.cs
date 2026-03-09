using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Service for managing the lifecycle of KGSM instances.
/// Controls the operational state and monitoring of game server instances.
/// </summary>
public interface ILifecycleService
{
    /// <summary>
    /// Launches a game server instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to start.</param>
    /// <returns>A <see cref="KgsmResult"/> containing the command execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult Start(string instanceName);

    /// <summary>
    /// Gracefully shuts down a running server instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to stop.</param>
    /// <returns>A <see cref="KgsmResult"/> containing the command execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult Stop(string instanceName);

    /// <summary>
    /// Performs a complete stop and start sequence for an instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to restart.</param>
    /// <returns>A <see cref="KgsmResult"/> containing the command execution result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult Restart(string instanceName);

    /// <summary>
    /// Displays comprehensive runtime status for an instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to get status for.</param>
    /// <returns>A <see cref="KgsmResult"/> containing the status information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult GetStatus(string instanceName);

    /// <summary>
    /// Checks if an instance is currently running.
    /// </summary>
    /// <param name="instanceName">The name of the instance to check.</param>
    /// <returns><c>true</c> if the instance is active; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    bool IsActive(string instanceName);

    /// <summary>
    /// Retrieves log entries for an instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to get logs for.</param>
    /// <param name="lines">The number of log lines to retrieve. Defaults to 10.</param>
    /// <returns>A collection of log lines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the logs cannot be retrieved.</exception>
    ICollection<string> GetLogs(string instanceName, int lines = 10);

    /// <summary>
    /// Retrieves log entries for an instance asynchronously.
    /// </summary>
    /// <param name="instanceName">The name of the instance to get logs for.</param>
    /// <param name="lines">The number of log lines to retrieve. Defaults to 10.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of log lines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the logs cannot be retrieved.</exception>
    Task<ICollection<string>> GetLogsAsync(string instanceName, int lines = 10, CancellationToken cancellationToken = default);
}
