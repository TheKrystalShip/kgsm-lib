using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the ILifecycleService interface for managing instance lifecycle operations in KGSM.
/// Controls the operational state and monitoring of game server instances.
/// </summary>
public class LifecycleService : ILifecycleService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<LifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the LifecycleService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public LifecycleService(
        IKgsmCommandExecutor commandExecutor,
        ILogger<LifecycleService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("LifecycleService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult Start(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Starting instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--start");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully started instance {InstanceName}", instanceName);
        }
        else
        {
            _logger.LogError("Failed to start instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult Stop(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Stopping instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--stop");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully stopped instance {InstanceName}", instanceName);
        }
        else
        {
            _logger.LogError("Failed to stop instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult Restart(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Restarting instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--restart");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully restarted instance {InstanceName}", instanceName);
        }
        else
        {
            _logger.LogError("Failed to restart instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting status for instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--status");

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to get status for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return result;
    }

    /// <inheritdoc/>
    public bool IsActive(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Checking if instance {InstanceName} is active", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--is-active");

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to check if instance {InstanceName} is active: {Error}", instanceName, result.Stderr);
            return false;
        }

        bool isActive = !result.Stdout.Contains("Inactive");
        _logger.LogDebug("Instance {InstanceName} is {Status}", instanceName, isActive ? "active" : "inactive");

        return isActive;
    }

    /// <inheritdoc/>
    public ICollection<string> GetLogs(string instanceName, int lines = 10)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting logs for instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--logs");

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to get logs for instance {InstanceName}: {Error}", instanceName, result.Stderr);
            throw new InvalidOperationException($"Failed to get logs for instance '{instanceName}': {result.Stderr}");
        }

        return result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetLogsAsync(string instanceName, int lines = 10, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting logs asynchronously for instance {InstanceName}", instanceName);

        KgsmResult result = await _commandExecutor.ExecuteAsync(["--instance", instanceName, "--logs"], cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to get logs for instance {InstanceName}: {Error}", instanceName, result.Stderr);
            throw new InvalidOperationException($"Failed to get logs for instance '{instanceName}': {result.Stderr}");
        }

        _logger.LogDebug("Successfully got logs for instance {InstanceName}", instanceName);
        return result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
