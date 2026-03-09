using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IInstanceService interface for managing instances in KGSM.
/// </summary>
public class InstanceService : IInstanceService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogSubscriptionService _logSubscriptionService;
    private readonly ILifecycleService _lifecycleService;
    private readonly ILogger<InstanceService> _logger;

    /// <summary>
    /// Initializes a new instance of the InstanceService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logSubscriptionService">The log subscription service for managing log streams.</param>
    /// <param name="lifecycleService">The lifecycle service for managing instance lifecycle operations.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public InstanceService(
        IKgsmCommandExecutor commandExecutor,
        ILogSubscriptionService logSubscriptionService,
        ILifecycleService lifecycleService,
        ILogger<InstanceService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logSubscriptionService = logSubscriptionService ?? throw new ArgumentNullException(nameof(logSubscriptionService));
        _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("InstanceService initialized");
    }

    /// <inheritdoc/>
    public Dictionary<string, Instance> GetAll()
    {
        _logger.LogDebug("Getting all instances");

        Dictionary<string, Instance>? instances =
            _commandExecutor.ExecuteForJson<Dictionary<string, Instance>>(["--instances", "--detailed", "--json"]) ?? new();

        _logger.LogDebug("Found {Count} instances", instances.Count);
        return instances;
    }

    /// <inheritdoc/>
    public Instance? GetInstanceInfo(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting detailed info for instance {InstanceName}", instanceName);

        return _commandExecutor.ExecuteForJson<Instance>(["--instance", instanceName, "--info", "--json"]);
    }

    /// <inheritdoc/>
    public InstanceRuntimeStatus? GetInstanceStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting status for instance {InstanceName}", instanceName);

        return _commandExecutor.ExecuteForJson<InstanceRuntimeStatus>(["--instance", instanceName, "--status", "--json"]);
    }

    /// <inheritdoc/>
    public KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(blueprintName, nameof(blueprintName));

        _logger.LogDebug("Installing instance of blueprint {Blueprint}", blueprintName);

        List<string> args = ["--create", blueprintName];

        if (installDir is not null)
        {
            args.Add("--install-dir");
            args.Add(installDir);
        }

        if (version is not null)
        {
            args.Add("--version");
            args.Add(version);
        }

        if (name is not null)
        {
            args.Add("--name");
            args.Add(name);
        }

        KgsmResult result = _commandExecutor.Execute(args.ToArray());

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully installed instance of blueprint {Blueprint}", blueprintName);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult Uninstall(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Uninstalling instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--uninstall", instanceName);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully uninstalled instance {InstanceName}", instanceName);
        }

        return result;
    }

    /// <inheritdoc/>
    public ICollection<string> GetLogs(string instanceName, int lines = 10)
    {
        return _lifecycleService.GetLogs(instanceName, lines);
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetLogsAsync(string instanceName, int lines = 10, CancellationToken cancellationToken = default)
    {
        return await _lifecycleService.GetLogsAsync(instanceName, lines, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus(string instanceName)
    {
        return _lifecycleService.GetStatus(instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetInfo(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting info for instance {InstanceName}", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--info");
    }

    /// <inheritdoc/>
    public bool IsActive(string instanceName)
    {
        return _lifecycleService.IsActive(instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult Start(string instanceName)
    {
        return _lifecycleService.Start(instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult Stop(string instanceName)
    {
        return _lifecycleService.Stop(instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult Restart(string instanceName)
    {
        return _lifecycleService.Restart(instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetInstalledVersion(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting installed version for instance {InstanceName}", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--version", "--installed");
    }

    /// <inheritdoc/>
    public KgsmResult GetLatestVersion(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting latest version for instance {InstanceName}", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--version", "--latest");
    }

    /// <inheritdoc/>
    public KgsmResult CheckUpdate(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Checking for updates for instance {InstanceName}", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--check-update");
    }

    /// <inheritdoc/>
    public KgsmResult Update(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Updating instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--update");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully updated instance {InstanceName}", instanceName);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult GetBackups(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting backups for instance {InstanceName}", instanceName);

        return _commandExecutor.Execute("--instance", instanceName, "--backups");
    }

    /// <inheritdoc/>
    public KgsmResult CreateBackup(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Creating backup for instance {InstanceName}", instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--create-backup");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully created backup for instance {InstanceName}", instanceName);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult RestoreBackup(string instanceName, string backupName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));
        ArgumentNullException.ThrowIfNull(backupName, nameof(backupName));

        _logger.LogDebug("Restoring backup {BackupName} for instance {InstanceName}", backupName, instanceName);

        KgsmResult result = _commandExecutor.Execute("--instance", instanceName, "--restore-backup", backupName);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully restored backup {BackupName} for instance {InstanceName}", backupName, instanceName);
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<LogSubscription> SubscribeToLogsAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        return _logSubscriptionService
            .SubscribeToLogsAsync(instanceName, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LogSubscription> SubscribeToLogsAsync(string instanceName, Core.Models.Enums.LogLevel minimumLogLevel, bool includeRawLines = true, CancellationToken cancellationToken = default)
    {
        return _logSubscriptionService
            .SubscribeToLogsAsync(instanceName, minimumLogLevel, includeRawLines, cancellationToken);
    }
}
