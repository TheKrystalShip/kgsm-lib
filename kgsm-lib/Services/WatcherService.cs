using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IWatcherService interface for managing watcher operations in KGSM.
/// Monitors game server instances to detect when they become ready for players.
/// </summary>
public class WatcherService : IWatcherService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<WatcherService> _logger;

    /// <summary>
    /// Initializes a new instance of the WatcherService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public WatcherService(
        IKgsmCommandExecutor commandExecutor,
        ILogger<WatcherService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("WatcherService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult StartWatch(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("watcher", "start", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult TestLogWatch(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("watcher", "logs", "test", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult TestPortWatch(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("watcher", "ports", "test", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("watcher", "status", instanceName);
    }
}
