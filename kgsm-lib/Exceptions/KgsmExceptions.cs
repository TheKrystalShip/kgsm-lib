namespace TheKrystalShip.KGSM.Exceptions;

/// <summary>
/// Base exception for all KGSM-related exceptions.
/// </summary>
public class KgsmException : Exception
{
    /// <summary>
    /// Initializes a new instance of the KgsmException class.
    /// </summary>
    public KgsmException() { }

    /// <summary>
    /// Initializes a new instance of the KgsmException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public KgsmException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the KgsmException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public KgsmException(string message, Exception innerException) : base(message, innerException) { }
}
