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
    }

    /// <inheritdoc/>
    public KgsmResult Create(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogInformation("Creating directory structure for instance {InstanceName}", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--directories", "create");
    }

    /// <inheritdoc/>
    public KgsmResult Remove(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogWarning("Removing directory structure for instance {InstanceName} - this will delete all instance data", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--directories", "remove");
    }
}
