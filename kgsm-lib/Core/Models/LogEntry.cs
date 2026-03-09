using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Models;

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
