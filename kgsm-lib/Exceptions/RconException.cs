namespace TheKrystalShip.KGSM.Exceptions;

/// <summary>
/// Exception thrown when an RCON operation fails (connection, authentication, or command execution).
/// </summary>
public class RconException : KgsmException
{
    /// <summary>
    /// Initializes a new instance of the RconException class.
    /// </summary>
    public RconException() { }

    /// <summary>
    /// Initializes a new instance of the RconException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RconException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the RconException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public RconException(string message, Exception innerException) : base(message, innerException) { }
}
