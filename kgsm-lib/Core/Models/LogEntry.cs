namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Represents the severity level of a log entry.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Trace level logging - most detailed information.
    /// </summary>
    Trace,

    /// <summary>
    /// Debug level logging - detailed information for debugging.
    /// </summary>
    Debug,

    /// <summary>
    /// Information level logging - general information.
    /// </summary>
    Info,

    /// <summary>
    /// Warning level logging - potentially harmful situations.
    /// </summary>
    Warning,

    /// <summary>
    /// Error level logging - error events that might still allow the application to continue.
    /// </summary>
    Error,

    /// <summary>
    /// Fatal level logging - very severe error events that will presumably lead to application abort.
    /// </summary>
    Fatal
}

/// <summary>
/// Represents a single log entry from an instance.
/// </summary>
public record LogEntry
{
    /// <summary>
    /// Gets or sets the timestamp when the log entry was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the severity level of the log entry.
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.Info;

    /// <summary>
    /// Gets or sets the log message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the instance that generated this log entry.
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw log line as received from KGSM.
    /// </summary>
    public string RawLine { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source component that generated the log entry.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the thread ID if available.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Returns a string representation of the log entry.
    /// </summary>
    /// <returns>A formatted string representation of the log entry.</returns>
    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{InstanceName}] {Message}";
    }
}
