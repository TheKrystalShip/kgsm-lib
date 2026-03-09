namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Provides data for log streaming events.
/// </summary>
public class LogStreamEventArgs : EventArgs
{
    /// <summary>
    /// Gets the log entry that was received.
    /// </summary>
    public LogEntry LogEntry { get; }

    /// <summary>
    /// Initializes a new instance of the LogStreamEventArgs class.
    /// </summary>
    /// <param name="logEntry">The log entry that was received.</param>
    public LogStreamEventArgs(LogEntry logEntry)
    {
        LogEntry = logEntry ?? throw new ArgumentNullException(nameof(logEntry));
    }
}

/// <summary>
/// Provides data for log stream error events.
/// </summary>
public class LogStreamErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name of the instance where the error occurred.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Initializes a new instance of the LogStreamErrorEventArgs class.
    /// </summary>
    /// <param name="instanceName">The name of the instance where the error occurred.</param>
    /// <param name="exception">The exception that occurred.</param>
    public LogStreamErrorEventArgs(string instanceName, Exception exception)
    {
        InstanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }
}

/// <summary>
/// Provides data for log stream status change events.
/// </summary>
public class LogStreamStatusEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name of the instance.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets a value indicating whether the log stream is connected.
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// Gets the optional status message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Initializes a new instance of the LogStreamStatusEventArgs class.
    /// </summary>
    /// <param name="instanceName">The name of the instance.</param>
    /// <param name="isConnected">Whether the log stream is connected.</param>
    /// <param name="message">Optional status message.</param>
    public LogStreamStatusEventArgs(string instanceName, bool isConnected, string? message = null)
    {
        InstanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
        IsConnected = isConnected;
        Message = message;
    }
}
