namespace TheKrystalShip.KGSM.Exceptions;

/// <summary>
/// Exception thrown when a backup operation fails.
/// </summary>
public class BackupException : InstanceException
{
    /// <summary>
    /// Gets the name of the backup associated with the exception.
    /// </summary>
    public string? BackupName { get; }

    /// <summary>
    /// Initializes a new instance of the BackupException class.
    /// </summary>
    public BackupException() { }

    /// <summary>
    /// Initializes a new instance of the BackupException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BackupException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BackupException class with a specified error message, instance name, and backup name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="instanceName">The name of the instance associated with the exception.</param>
    /// <param name="backupName">The name of the backup associated with the exception.</param>
    public BackupException(string message, string instanceName, string backupName) : base(message, instanceName)
    {
        BackupName = backupName;
    }

    /// <summary>
    /// Initializes a new instance of the BackupException class with a specified error message, instance name, backup name, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="instanceName">The name of the instance associated with the exception.</param>
    /// <param name="backupName">The name of the backup associated with the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public BackupException(string message, string instanceName, string backupName, Exception innerException) : base(message, instanceName, innerException)
    {
        BackupName = backupName;
    }
}
