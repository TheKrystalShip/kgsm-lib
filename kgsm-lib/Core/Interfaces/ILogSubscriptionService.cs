using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Provides log subscription capabilities for KGSM instances with real-time streaming,
/// filtering, and event-based notification.
/// </summary>
public interface ILogSubscriptionService
{
    /// <summary>
    /// Subscribes to continuous log streaming for an instance with default settings.
    /// </summary>
    /// <param name="instanceName">The name of the instance to stream logs from.</param>
    /// <param name="cancellationToken">Optional cancellation token to stop the log streaming.</param>
    /// <returns>A Task that resolves to a LogSubscription object for managing the log stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when instanceName is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the KGSM process fails to start.</exception>
    /// <remarks>
    /// <para>
    /// This method starts a background process that continuously streams logs from the KGSM instance.
    /// The returned LogSubscription object allows you to:
    /// - Subscribe to log events via the LogReceived event
    /// - Monitor streaming status via StatusChanged event
    /// - Handle errors via ErrorOccurred event
    /// - Control the stream via StopAsync() method
    /// </para>
    /// <para>
    /// When disposed, it will automatically stop the underlying KGSM process and clean up all resources.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var subscription = await logSubscriptionService.SubscribeToLogsAsync("my-server");
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
    /// <param name="includeRawLines">Whether to include raw log lines in addition to parsed entries. Set to false to reduce memory usage.</param>
    /// <param name="cancellationToken">Optional cancellation token to stop the log streaming.</param>
    /// <returns>A Task that resolves to a LogSubscription object for managing the log stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when instanceName is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the KGSM process fails to start.</exception>
    /// <remarks>
    /// This method provides additional filtering capabilities:
    /// - minimumLogLevel: Only log entries at or above this level will be included
    /// - includeRawLines: When true, the LogEntry.RawLine property will contain the original log line
    /// </remarks>
    Task<LogSubscription> SubscribeToLogsAsync(
        string instanceName,
        LogLevel minimumLogLevel,
        bool includeRawLines = true,
        CancellationToken cancellationToken = default);
}
