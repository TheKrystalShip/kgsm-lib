using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IDirectoryService interface for managing directory operations in KGSM.
/// Creates and manages the directory structure needed for game server instances.
/// </summary>
public class DirectoryService : IDirectoryService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<DirectoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the DirectoryService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public DirectoryService(
        IKgsmCommandExecutor commandExecutor,
        ILogger<DirectoryService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("DirectoryService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult Create(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("directories", "create", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult Remove(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("directories", "remove", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult LinkInstance(string blueprint, string instanceName, string workingDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprint, nameof(blueprint));
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDir, nameof(workingDir));

        return _commandExecutor.Execute("directories", "link-instance", blueprint, instanceName, workingDir);
    }

    /// <inheritdoc/>
    public KgsmResult UnlinkInstance(string blueprint, string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprint, nameof(blueprint));
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("directories", "unlink-instance", blueprint, instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult EnsureCreated(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        return _commandExecutor.Execute("directories", "ensure-created", path);
    }
}
