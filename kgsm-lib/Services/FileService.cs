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
        ExecuteFileOperation(instanceName, "create");

    /// <inheritdoc/>
    public KgsmResult CreateManage(string instanceName) =>
        ExecuteFileOperation(instanceName, "management", "create");

    /// <inheritdoc/>
    [Obsolete("kgsm removed the standalone 'files config' component; the config file is created by Create(). "
        + "Issues a command kgsm no longer routes. Scheduled for removal in the next major version.")]
    public KgsmResult CreateConfig(string instanceName) =>
        ExecuteFileOperation(instanceName, "config", "install");

    /// <inheritdoc/>
    public KgsmResult CreateSystemd(string instanceName) =>
        ExecuteFileOperation(instanceName, "systemd", "enable");

    /// <inheritdoc/>
    public KgsmResult CreateFirewall(string instanceName) =>
        ExecuteFileOperation(instanceName, "firewall", "enable");

    /// <inheritdoc/>
    public KgsmResult CreateSymlink(string instanceName) =>
        ExecuteFileOperation(instanceName, "symlink", "enable");

    /// <inheritdoc/>
    public KgsmResult CreateUpnp(string instanceName) =>
        ExecuteFileOperation(instanceName, "upnp", "enable");

    /// <inheritdoc/>
    public KgsmResult Remove(string instanceName) =>
        ExecuteFileOperation(instanceName, "remove");

    /// <inheritdoc/>
    public KgsmResult RemoveSystemd(string instanceName) =>
        ExecuteFileOperation(instanceName, "systemd", "disable");

    /// <inheritdoc/>
    public KgsmResult RemoveFirewall(string instanceName) =>
        ExecuteFileOperation(instanceName, "firewall", "disable");

    /// <inheritdoc/>
    public KgsmResult RemoveSymlink(string instanceName) =>
        ExecuteFileOperation(instanceName, "symlink", "disable");

    /// <inheritdoc/>
    public KgsmResult RemoveUpnp(string instanceName) =>
        ExecuteFileOperation(instanceName, "upnp", "disable");

    /// <inheritdoc/>
    [Obsolete("kgsm removed the standalone 'files config' component; config-file cleanup is handled by Remove(). "
        + "Issues a command kgsm no longer routes. Scheduled for removal in the next major version.")]
    public KgsmResult RemoveConfig(string instanceName) =>
        ExecuteFileOperation(instanceName, "config", "uninstall");

    /// <inheritdoc/>
    public KgsmResult RemoveManage(string instanceName) =>
        ExecuteFileOperation(instanceName, "management", "remove");

    /// <summary>
    /// Validates the instance name and executes a new-style file command of the form
    /// <c>files [component] &lt;verb&gt; &lt;instance&gt;</c>.
    /// </summary>
    /// <param name="instanceName">Instance name to perform the operation on.</param>
    /// <param name="command">
    /// The command tokens that precede the instance name. Either a single quick-command
    /// verb (e.g. <c>create</c>, <c>remove</c>) or a component and its verb
    /// (e.g. <c>systemd</c>, <c>enable</c>).
    /// </param>
    /// <returns>Result of the file operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the instance name is null or whitespace.</exception>
    private KgsmResult ExecuteFileOperation(string instanceName, params string[] command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        string[] args = ["files", .. command, instanceName];
        return _commandExecutor.Execute(args);
    }
}
