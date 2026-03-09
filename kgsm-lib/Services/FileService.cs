using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IFileService interface for managing file operations in KGSM.
/// Creates and manages all necessary files for game server operation.
/// </summary>
public class FileService : IFileService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<FileService> _logger;

    /// <summary>
    /// Initializes a new instance of the FileService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public FileService(
        IKgsmCommandExecutor commandExecutor,
        ILogger<FileService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("FileService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult Create(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Creating all files");

    /// <inheritdoc/>
    public KgsmResult CreateManage(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Creating management script", "--manage");

    /// <inheritdoc/>
    public KgsmResult CreateConfig(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Copying configuration file", "--config");

    /// <inheritdoc/>
    public KgsmResult CreateSystemd(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Creating systemd files", "--systemd");

    /// <inheritdoc/>
    public KgsmResult CreateUfw(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Creating UFW firewall rule", "--ufw");

    /// <inheritdoc/>
    public KgsmResult CreateSymlink(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Creating symlink", "--symlink");

    /// <inheritdoc/>
    public KgsmResult CreateUpnp(string instanceName) =>
        ExecuteFileOperation(instanceName, "--create", "Creating UPnP configuration", "--upnp");

    /// <inheritdoc/>
    public KgsmResult Remove(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing all files");

    /// <inheritdoc/>
    public KgsmResult RemoveSystemd(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing systemd files", "--systemd");

    /// <inheritdoc/>
    public KgsmResult RemoveUfw(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing UFW firewall rules", "--ufw");

    /// <inheritdoc/>
    public KgsmResult RemoveSymlink(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing symlink", "--symlink");

    /// <inheritdoc/>
    public KgsmResult RemoveUpnp(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing UPnP configuration", "--upnp");

    /// <inheritdoc/>
    public KgsmResult RemoveConfig(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing configuration file", "--config");

    /// <inheritdoc/>
    public KgsmResult RemoveManage(string instanceName) =>
        ExecuteFileOperation(instanceName, "--remove", "Removing management file", "--manage");

    /// <summary>
    /// Executes a file operation with centralized parameter validation, logging, and command execution.
    /// </summary>
    /// <param name="instanceName">Instance name to perform the operation on.</param>
    /// <param name="operation">The operation type (--create or --remove).</param>
    /// <param name="logMessage">The log message describing the operation.</param>
    /// <param name="subCommand">Optional subcommand (e.g., --manage, --systemd).</param>
    /// <returns>Result of the file operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
    private KgsmResult ExecuteFileOperation(string instanceName, string operation, string logMessage, string? subCommand = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        ArgumentNullException.ThrowIfNull(logMessage, nameof(logMessage));

        _logger.LogInformation("{Message} for instance {InstanceName}", logMessage, instanceName);

        return subCommand is null
            ? _commandExecutor.Execute("--instance", instanceName, operation)
            : _commandExecutor.Execute("--instance", instanceName, operation, subCommand);
    }
}
