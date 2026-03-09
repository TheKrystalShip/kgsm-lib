using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing instances in KGSM.
/// </summary>
public interface IInstanceService
{
    /// <summary>
    /// Gets a dictionary of all instances.
    /// </summary>
    /// <returns>A dictionary of instance names to instance objects.</returns>
    Dictionary<string, Instance> GetAll();

    /// <summary>
    /// Gets detailed information about a specific instance in JSON format.
    /// </summary>
    /// <param name="instanceName">Instance name to get information for.</param>
    /// <returns>The instance information as a structured object.</returns>
    Instance? GetInstanceInfo(string instanceName);

    /// <summary>
    /// Gets the runtime status summary for a specific instance in JSON format.
    /// </summary>
    /// <param name="instanceName">Instance name to get status for.</param>
    /// <returns>The instance status as a structured object.</returns>
    InstanceRuntimeStatus? GetInstanceStatus(string instanceName);

    /// <summary>
    /// Installs an instance of a blueprint.
    /// </summary>
    /// <param name="blueprintName">Name of the blueprint to install.</param>
    /// <param name="installDir">Optional installation directory.</param>
    /// <param name="version">Optional version to install.</param>
    /// <param name="name">Optional identifier used when creating the instance.</param>
    /// <returns>Result of the instance installation operation.</returns>
    KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null);

    /// <summary>
    /// Uninstalls an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to uninstall.</param>
    /// <returns>Result of the uninstallation operation.</returns>
    KgsmResult Uninstall(string instanceName);

    /// <summary>
    /// Gets the logs for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get logs for.</param>
    /// <param name="maxLines">Maximum number of log lines to retrieve. Default is 10.</param>
    /// <returns>Result containing the instance logs.</returns>
    ICollection<string> GetLogs(string instanceName, int maxLines = 10);

    /// <summary>
    /// Gets the logs for an instance asynchronously.
    /// </summary>
    /// <param name="instanceName">Instance name to get logs for.</param>
    /// <param name="maxLines">Maximum number of log lines to retrieve. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the instance logs.</returns>
    Task<ICollection<string>> GetLogsAsync(string instanceName, int maxLines = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get status for.</param>
    /// <returns>Result containing the instance status.</returns>
    KgsmResult GetStatus(string instanceName);

    /// <summary>
    /// Gets information about an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get information for.</param>
    /// <returns>Result containing the instance information.</returns>
    KgsmResult GetInfo(string instanceName);

    /// <summary>
    /// Checks if an instance is currently active/running.
    /// </summary>
    /// <param name="instanceName">Instance name to check.</param>
    /// <returns>True if the instance is active, false otherwise.</returns>
    bool IsActive(string instanceName);

    /// <summary>
    /// Starts an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to start.</param>
    /// <returns>Result of the start operation.</returns>
    KgsmResult Start(string instanceName);

    /// <summary>
    /// Stops an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to stop.</param>
    /// <returns>Result of the stop operation.</returns>
    KgsmResult Stop(string instanceName);

    /// <summary>
    /// Restarts an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to restart.</param>
    /// <returns>Result of the restart operation.</returns>
    KgsmResult Restart(string instanceName);

    /// <summary>
    /// Gets the installed version of an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get version for.</param>
    /// <returns>Result containing the installed version.</returns>
    KgsmResult GetInstalledVersion(string instanceName);

    /// <summary>
    /// Gets the latest available version for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get latest version for.</param>
    /// <returns>Result containing the latest version.</returns>
    KgsmResult GetLatestVersion(string instanceName);

    /// <summary>
    /// Checks if there's an update available for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to check for updates.</param>
    /// <returns>Result indicating if an update is available.</returns>
    KgsmResult CheckUpdate(string instanceName);

    /// <summary>
    /// Updates an instance to the latest version.
    /// </summary>
    /// <param name="instanceName">Instance name to update.</param>
    /// <returns>Result of the update operation.</returns>
    KgsmResult Update(string instanceName);

    /// <summary>
    /// Gets a list of backups for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get backups for.</param>
    /// <returns>Result containing the list of backups.</returns>
    KgsmResult GetBackups(string instanceName);

    /// <summary>
    /// Creates a backup for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to create backup for.</param>
    /// <returns>Result of the backup creation operation.</returns>
    KgsmResult CreateBackup(string instanceName);

    /// <summary>
    /// Restores a backup for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to restore backup for.</param>
    /// <param name="backupName">Name of the backup to restore.</param>
    /// <returns>Result of the backup restoration operation.</returns>
    KgsmResult RestoreBackup(string instanceName, string backupName);

    /// <summary>
    /// Subscribes to continuous log streaming for an instance.
    /// This method starts a background process that continuously streams logs from the specified instance
    /// using the KGSM "--follow" flag. The returned LogSubscription object provides events for
    /// receiving log entries, handling errors, and monitoring connection status.
    /// </summary>
    /// <param name="instanceName">The name of the instance to stream logs from.</param>
    /// <param name="cancellationToken">Optional cancellation token to stop the log streaming.</param>
    /// <returns>
    /// A Task that resolves to a LogSubscription object which can be used to:
    /// - Subscribe to log events via the LogReceived event
    /// - Handle errors via the ErrorOccurred event
    /// - Monitor connection status via the StatusChanged event
    /// - Stop the streaming by calling StopAsync() or disposing the subscription
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when instanceName is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the KGSM process fails to start.</exception>
    /// <remarks>
    /// <para>
    /// This method executes the KGSM command: "--instance {instanceName} --logs --follow"
    /// which provides continuous log output until manually stopped.
    /// </para>
    /// <para>
    /// The LogSubscription implements IDisposable and should be properly disposed to clean up resources.
    /// When disposed, it will automatically stop the underlying KGSM process and clean up all resources.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var subscription = await instanceService.SubscribeToLogsAsync("my-server");
    /// subscription.LogReceived += (sender, args) =>
    /// {
    ///     Console.WriteLine($"[{args.LogEntry.Timestamp}] {args.LogEntry.Message}");
    /// };
    ///
    /// // Later, stop the subscription
    /// await subscription.StopAsync();
    /// subscription.Dispose();
    /// </code>
    /// </para>
    /// </remarks>
    Task<LogSubscription> SubscribeToLogsAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to continuous log streaming for an instance with filtering options.
    /// This overload allows you to specify log level filtering and custom parsing options.
    /// </summary>
    /// <param name="instanceName">The name of the instance to stream logs from.</param>
    /// <param name="minimumLogLevel">The minimum log level to include in the stream. Logs below this level will be filtered out.</param>
    /// <param name="includeRawLines">Whether to include raw log lines in addition to parsed entries.</param>
    /// <param name="cancellationToken">Optional cancellation token to stop the log streaming.</param>
    /// <returns>A Task that resolves to a LogSubscription object for managing the log stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when instanceName is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the KGSM process fails to start.</exception>
    /// <remarks>
    /// This method provides additional filtering capabilities:
    /// - minimumLogLevel: Only log entries at or above this level will be included
    /// - includeRawLines: When true, the LogEntry.RawLine property will contain the original log line
    /// </remarks>
    Task<LogSubscription> SubscribeToLogsAsync(string instanceName, LogLevel minimumLogLevel, bool includeRawLines = true, CancellationToken cancellationToken = default);
}
