namespace TheKrystalShip.KGSM.Exceptions;

/// <summary>
/// Exception thrown when a blueprint operation fails.
/// </summary>
public class BlueprintException : KgsmException
{
    /// <summary>
    /// Gets the name of the blueprint associated with the exception.
    /// </summary>
    public string? BlueprintName { get; }

    /// <summary>
    /// Initializes a new instance of the BlueprintException class.
    /// </summary>
    public BlueprintException() { }

    /// <summary>
    /// Initializes a new instance of the BlueprintException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BlueprintException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BlueprintException class with a specified error message and blueprint name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="blueprintName">The name of the blueprint associated with the exception.</param>
    public BlueprintException(string message, string blueprintName) : base(message)
    {
        BlueprintName = blueprintName;
    }

    /// <summary>
    /// Initializes a new instance of the BlueprintException class with a specified error message, blueprint name, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="blueprintName">The name of the blueprint associated with the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public BlueprintException(string message, string blueprintName, Exception innerException) : base(message, innerException)
    {
        BlueprintName = blueprintName;
    }
}
