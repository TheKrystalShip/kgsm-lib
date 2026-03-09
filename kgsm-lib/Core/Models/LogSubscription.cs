using System.Diagnostics;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Represents a subscription to continuous log streaming for an instance.
/// Provides methods to control the log stream and handle cleanup.
/// </summary>
public class LogSubscription : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Process? _process;
    private readonly Task _streamingTask;
    private bool _disposed;

        /// <summary>
    /// Initializes a new instance of the LogSubscription class.
    /// </summary>
    /// <param name="instanceName">The name of the instance being monitored.</param>
    /// <param name="process">The KGSM process handling the log streaming.</param>
    /// <param name="streamingTask">The task that handles the log streaming.</param>
    /// <param name="cancellationTokenSource">The cancellation token source for stopping the stream.</param>
    public LogSubscription(
        string instanceName,
        Process? process,
        Task streamingTask,
        CancellationTokenSource cancellationTokenSource)
    {
        InstanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
        _process = process;
        _streamingTask = streamingTask ?? throw new ArgumentNullException(nameof(streamingTask));
        _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));

        SubscriptionId = Guid.NewGuid();
        StartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the unique identifier for this subscription.
    /// </summary>
    public Guid SubscriptionId { get; }

    /// <summary>
    /// Gets the name of the instance being monitored.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets the time when this subscription was started.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets a value indicating whether the subscription is currently active.
    /// </summary>
    public bool IsActive => !_cancellationTokenSource.Token.IsCancellationRequested &&
                           !_streamingTask.IsCompleted &&
                           !_disposed;

    /// <summary>
    /// Gets a value indicating whether the underlying process is running.
    /// </summary>
    public bool IsProcessRunning
    {
        get
        {
            if (_process == null)
                return false;

            try
            {
                return !_process.HasExited;
            }
            catch (InvalidOperationException)
            {
                // Process is not in a valid state (e.g., not started or already disposed)
                return false;
            }
        }
    }

    /// <summary>
    /// Gets the current status of the streaming task.
    /// </summary>
    public TaskStatus StreamingTaskStatus => _streamingTask.Status;

    /// <summary>
    /// Event raised when a log entry is received.
    /// </summary>
    public event EventHandler<LogStreamEventArgs>? LogReceived;

    /// <summary>
    /// Event raised when an error occurs during log streaming.
    /// </summary>
    public event EventHandler<LogStreamErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Event raised when the log stream status changes.
    /// </summary>
    public event EventHandler<LogStreamStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Stops the log streaming asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync()
    {
        if (_disposed || _cancellationTokenSource.Token.IsCancellationRequested)
            return;

        try
        {
            // Cancel the streaming operation
            _cancellationTokenSource.Cancel();

            // Kill the process if it's still running
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                        await _process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process is not in a valid state (e.g., not started or already disposed)
                }
            }

            // Wait for the streaming task to complete with a timeout
            try
            {
                await _streamingTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Task didn't complete within timeout, but that's okay
            }

            OnStatusChanged(false, "Log streaming stopped");
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex);
        }
    }

    /// <summary>
    /// Raises the LogReceived event.
    /// </summary>
    /// <param name="logEntry">The log entry that was received.</param>
    public void OnLogReceived(LogEntry logEntry)
    {
        LogReceived?.Invoke(this, new LogStreamEventArgs(logEntry));
    }

    /// <summary>
    /// Raises the ErrorOccurred event.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    public void OnErrorOccurred(Exception exception)
    {
        ErrorOccurred?.Invoke(this, new LogStreamErrorEventArgs(InstanceName, exception));
    }

    /// <summary>
    /// Raises the StatusChanged event.
    /// </summary>
    /// <param name="isConnected">Whether the stream is connected.</param>
    /// <param name="message">Optional status message.</param>
    public void OnStatusChanged(bool isConnected, string? message = null)
    {
        StatusChanged?.Invoke(this, new LogStreamStatusEventArgs(InstanceName, isConnected, message));
    }

    /// <summary>
    /// Releases all resources used by the LogSubscription.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the LogSubscription and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Stop the streaming asynchronously, but don't wait for it
                _ = Task.Run(async () => await StopAsync().ConfigureAwait(false));
            }
            catch
            {
                // Ignore exceptions during disposal
            }

            _cancellationTokenSource?.Dispose();
            _process?.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Returns a string representation of the log subscription.
    /// </summary>
    /// <returns>A string that represents the current log subscription.</returns>
    public override string ToString()
    {
        return $"LogSubscription[{SubscriptionId:N}]: {InstanceName} - Active: {IsActive}, Started: {StartTime:yyyy-MM-dd HH:mm:ss}";
    }
}
