using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing instances in KGSM.
/// </summary>
/// <remarks>
/// Failure-channel convention: a method returning <see cref="KgsmResult"/> reports a
/// failed command in the result itself (<see cref="KgsmResult.IsSuccess"/> is false) and
/// does not throw on a non-zero exit; a method returning a nullable type returns null on
/// failure. Argument validation (e.g. a null instance name) always throws, regardless of
/// return type.
/// </remarks>
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
    /// Gets the runtime status of every instance in a single KGSM invocation.
    /// </summary>
    /// <param name="fast">
    /// When true, passes <c>--fast</c> so KGSM skips the per-instance network
    /// update-check (roughly a 20x speed-up on the fleet). In fast mode each
    /// instance's <see cref="VersionInfo.Latest"/> and
    /// <see cref="VersionInfo.UpdatesAvailable"/> are null and
    /// <see cref="VersionInfo.Checked"/> is false — KGSM reports "unchecked"
    /// rather than fabricating an answer.
    /// </param>
    /// <returns>
    /// A map of instance name to a <see cref="Reading{T}"/> of its runtime
    /// status. A successfully-read instance is <see cref="ReadingState.Measured"/>
    /// with its status in <see cref="Reading{T}.Value"/>; an instance whose status
    /// could not be read (e.g. a management file that cannot answer
    /// <c>--status</c>) is <see cref="ReadingState.Unavailable"/> with a
    /// <see cref="ReadingCode"/> (typically <see cref="ReadingCode.RequiresRegeneration"/>),
    /// so one bad instance never sinks the whole call. Empty if the command itself
    /// fails.
    /// </returns>
    /// <remarks>
    /// This is the bulk counterpart to <see cref="GetInstanceStatus(string)"/>:
    /// it bootstraps KGSM once instead of once per instance, which is the
    /// intended replacement for fanning a per-instance status loop out across
    /// the fleet. The <see cref="InstanceRuntimeStatus.Status"/> here is the
    /// instance management script's own status report; dedicated liveness
    /// routing (systemd vs standalone) is a separate lifecycle-layer concern.
    /// </remarks>
    Dictionary<string, Reading<InstanceRuntimeStatus>> GetAllStatuses(bool fast = false);

    /// <summary>
    /// Installs an instance of a blueprint.
    /// </summary>
    /// <param name="blueprintName">Name of the blueprint to install.</param>
    /// <param name="installDir">Optional installation directory.</param>
    /// <param name="version">Optional version to install.</param>
    /// <param name="name">Optional identifier used when creating the instance.</param>
    /// <param name="actor">Optional audit principal (who) propagated to KGSM as <c>KGSM_EVENT_ACTOR</c>
    /// so the emitted event is attributable; null/empty = KGSM's OS-user fallback (never fabricated).</param>
    /// <param name="origin">Optional driving surface (through-what) propagated as <c>KGSM_EVENT_ORIGIN</c>;
    /// null/empty = no surface emitted.</param>
    /// <returns>Result of the instance installation operation.</returns>
    KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null, string? actor = null, string? origin = null);

    /// <summary>
    /// Uninstalls an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to uninstall.</param>
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the uninstallation operation.</returns>
    KgsmResult Uninstall(string instanceName, string? actor = null, string? origin = null);

    /// <summary>
    /// Gets the logs for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get logs for.</param>
    /// <param name="maxLines">Maximum number of log lines to retrieve. Default is 10.</param>
    /// <returns>Result containing the instance logs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the logs cannot be retrieved.</exception>
    ICollection<string> GetLogs(string instanceName, int maxLines = 10);

    /// <summary>
    /// Gets the logs for an instance asynchronously.
    /// </summary>
    /// <param name="instanceName">Instance name to get logs for.</param>
    /// <param name="maxLines">Maximum number of log lines to retrieve. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the instance logs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the logs cannot be retrieved.</exception>
    Task<ICollection<string>> GetLogsAsync(string instanceName, int maxLines = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get status for.</param>
    /// <returns>Result containing the instance status.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult GetStatus(string instanceName);

    /// <summary>
    /// Gets information about an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to get information for.</param>
    /// <returns>Result containing the instance information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult GetInfo(string instanceName);

    /// <summary>
    /// Checks if an instance is currently active/running.
    /// </summary>
    /// <param name="instanceName">Instance name to check.</param>
    /// <returns>True if the instance is active, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    bool IsActive(string instanceName);

    /// <summary>
    /// Starts an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to start.</param>
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>. Forwarded to the
    /// lifecycle layer so the emitted event is attributable.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the start operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult Start(string instanceName, string? actor = null, string? origin = null);

    /// <summary>
    /// Stops an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to stop.</param>
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the stop operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult Stop(string instanceName, string? actor = null, string? origin = null);

    /// <summary>
    /// Restarts an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to restart.</param>
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the restart operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceName"/> is null.</exception>
    KgsmResult Restart(string instanceName, string? actor = null, string? origin = null);

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
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the update operation.</returns>
    KgsmResult Update(string instanceName, string? actor = null, string? origin = null);

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
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the backup creation operation.</returns>
    KgsmResult CreateBackup(string instanceName, string? actor = null, string? origin = null);

    /// <summary>
    /// Restores a backup for an instance.
    /// </summary>
    /// <param name="instanceName">Instance name to restore backup for.</param>
    /// <param name="backupName">Name of the backup to restore.</param>
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the backup restoration operation.</returns>
    KgsmResult RestoreBackup(string instanceName, string backupName, string? actor = null, string? origin = null);

    /// <summary>
    /// Generates a unique instance identifier for a blueprint.
    /// If a custom name is provided and is valid and unique, returns that name.
    /// Otherwise, generates a name with format blueprint-suffix.
    /// </summary>
    /// <param name="blueprintName">The blueprint to generate an ID for.</param>
    /// <param name="customName">Optional custom name to use if valid and unique.</param>
    /// <returns>Result containing the generated or custom instance name.</returns>
    /// <exception cref="ArgumentException">Thrown when blueprintName is null or whitespace.</exception>
    KgsmResult GenerateId(string blueprintName, string? customName = null);

    /// <summary>
    /// Sends a save command to a running instance.
    /// Delegates to the instance's management file save command.
    /// </summary>
    /// <param name="instanceName">The instance to save.</param>
    /// <returns>Result of the save operation.</returns>
    /// <exception cref="ArgumentException">Thrown when instanceName is null or whitespace.</exception>
    KgsmResult Save(string instanceName);

    /// <summary>
    /// Sends a console command to a running instance.
    /// The command is sent to the instance's console and the last log lines are returned.
    /// </summary>
    /// <param name="instanceName">The instance to send the command to.</param>
    /// <param name="command">The console command to send.</param>
    /// <returns>Result containing log output after command execution.</returns>
    /// <exception cref="ArgumentException">Thrown when instanceName or command is null or whitespace.</exception>
    KgsmResult SendInput(string instanceName, string command);

    /// <summary>
    /// Gets the absolute path to an instance's configuration file.
    /// </summary>
    /// <param name="instanceName">The instance to find the config path for.</param>
    /// <returns>Result containing the absolute path to the config file.</returns>
    /// <exception cref="ArgumentException">Thrown when instanceName is null or whitespace.</exception>
    KgsmResult FindConfigPath(string instanceName);

    /// <summary>
    /// Reads a single value from an instance's configuration file.
    /// </summary>
    /// <param name="instanceName">The instance whose config to read.</param>
    /// <param name="key">The configuration key to read.</param>
    /// <returns>Result whose standard output holds the value (empty if the key is absent).</returns>
    /// <exception cref="ArgumentException">Thrown when instanceName or key is null or whitespace.</exception>
    KgsmResult GetInstanceConfigValue(string instanceName, string key);

    /// <summary>
    /// Sets a single key=value in an instance's configuration file.
    /// </summary>
    /// <remarks>
    /// Only plain runtime values are settable. KGSM refuses identity/structural keys,
    /// the filesystem paths it manages (every *_dir/*_file), and the integration toggles
    /// (enable_firewall_management, enable_command_shortcuts) —
    /// those have dedicated flows. A refused key surfaces as a non-zero
    /// <see cref="KgsmResult"/> rather than an exception.
    /// </remarks>
    /// <param name="instanceName">The instance whose config to modify.</param>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The value to write; may be the empty string, but not null.</param>
    /// <param name="actor">Optional audit principal — see <see cref="Install"/>.</param>
    /// <param name="origin">Optional driving surface — see <see cref="Install"/>.</param>
    /// <returns>Result of the operation.</returns>
    /// <exception cref="ArgumentException">Thrown when instanceName or key is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    KgsmResult SetInstanceConfigValue(string instanceName, string key, string value, string? actor = null, string? origin = null);

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
