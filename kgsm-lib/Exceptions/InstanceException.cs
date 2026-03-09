namespace TheKrystalShip.KGSM.Exceptions;

/// <summary>
/// Exception thrown when an instance operation fails.
/// </summary>
public class InstanceException : KgsmException
{
    /// <summary>
    /// Gets the name of the instance associated with the exception.
    /// </summary>
    public string? InstanceName { get; }

    /// <summary>
    /// Initializes a new instance of the InstanceException class.
    /// </summary>
    public InstanceException() { }

    /// <summary>
    /// Initializes a new instance of the InstanceException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InstanceException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the InstanceException class with a specified error message and instance name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="instanceName">The name of the instance associated with the exception.</param>
    public InstanceException(string message, string instanceName) : base(message)
    {
        InstanceName = instanceName;
    }

    /// <summary>
    /// Initializes a new instance of the InstanceException class with a specified error message, instance name, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="instanceName">The name of the instance associated with the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public InstanceException(string message, string instanceName, Exception innerException) : base(message, innerException)
    {
        InstanceName = instanceName;
    }
}
