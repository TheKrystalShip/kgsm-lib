using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Represents the severity level of a log entry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))]
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