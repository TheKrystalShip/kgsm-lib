namespace TheKrystalShip.KGSM.Exceptions;

/// <summary>
/// Exception thrown when a socket operation fails.
/// </summary>
public class SocketException : KgsmException
{
    /// <summary>
    /// Gets the path of the socket associated with the exception.
    /// </summary>
    public string? SocketPath { get; }

    /// <summary>
    /// Initializes a new instance of the SocketException class.
    /// </summary>
    public SocketException() { }

    /// <summary>
    /// Initializes a new instance of the SocketException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SocketException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the SocketException class with a specified error message and socket path.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="socketPath">The path of the socket associated with the exception.</param>
    public SocketException(string message, string socketPath) : base(message)
    {
        SocketPath = socketPath;
    }

    /// <summary>
    /// Initializes a new instance of the SocketException class with a specified error message, socket path, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="socketPath">The path of the socket associated with the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public SocketException(string message, string socketPath, Exception innerException) : base(message, innerException)
    {
        SocketPath = socketPath;
    }
}
