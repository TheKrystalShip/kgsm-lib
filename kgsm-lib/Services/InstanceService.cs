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
    private readonly KgsmTimeoutOptions _timeouts;
    private readonly ILogger<InstanceService> _logger;

    /// <summary>
    /// Initializes a new instance of the InstanceService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logSubscriptionService">The log subscription service for managing log streams.</param>
    /// <param name="lifecycleService">The lifecycle service for managing instance lifecycle operations.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="kgsmOptions">
    /// KGSM options, used here for the per-operation timeouts. Optional: when null
    /// (e.g. in tests that don't exercise timeouts), generous defaults are used.
    /// The DI container injects the registered instance.
    /// </param>
    public InstanceService(
        IKgsmCommandExecutor commandExecutor,
        ILogSubscriptionService logSubscriptionService,
        ILifecycleService lifecycleService,
        ILogger<InstanceService> logger,
        KgsmOptions? kgsmOptions = null)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logSubscriptionService = logSubscriptionService ?? throw new ArgumentNullException(nameof(logSubscriptionService));
        _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        _timeouts = kgsmOptions?.Timeouts ?? new KgsmTimeoutOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("InstanceService initialized");
    }

    /// <inheritdoc/>
    public Dictionary<string, Instance> GetAll()
    {
        return _commandExecutor.ExecuteForJson<Dictionary<string, Instance>>(["instances", "list", "--detailed", "--json"]) ?? [];
    }

    /// <inheritdoc/>
    public Instance? GetInstanceInfo(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.ExecuteForJson<Instance>(["instances", "info", instanceName, "--json"]);
    }

    /// <inheritdoc/>
    public InstanceRuntimeStatus? GetInstanceStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.ExecuteForJson<InstanceRuntimeStatus>(["instances", "status", instanceName, "--json"]);
    }

    /// <inheritdoc/>
    public Dictionary<string, InstanceRuntimeStatus> GetAllStatuses(bool fast = false)
    {
        string[] args = fast
            ? ["instances", "list", "--status", "--json", "--fast"]
            : ["instances", "list", "--status", "--json"];

        return _commandExecutor.ExecuteForJson<Dictionary<string, InstanceRuntimeStatus>>(args) ?? [];
    }

    /// <inheritdoc/>
    public KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(blueprintName, nameof(blueprintName));

        List<string> args = ["install", blueprintName];

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

        return _commandExecutor.Execute(_timeouts.Install, args.ToArray());
    }

    /// <inheritdoc/>
    public KgsmResult Uninstall(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute(_timeouts.Uninstall, "uninstall", instanceName);
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

        return _commandExecutor.Execute("instances", "info", instanceName);
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

        return _commandExecutor.Execute("instances", "version", instanceName, "--installed");
    }

    /// <inheritdoc/>
    public KgsmResult GetLatestVersion(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "version", instanceName, "--latest");
    }

    /// <inheritdoc/>
    public KgsmResult CheckUpdate(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "check-update", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult Update(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute(_timeouts.Update, "instances", "update", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetBackups(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "backups", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult CreateBackup(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute(_timeouts.Backup, "instances", "create-backup", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult RestoreBackup(string instanceName, string backupName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));
        ArgumentNullException.ThrowIfNull(backupName, nameof(backupName));

        return _commandExecutor.Execute(_timeouts.Restore, "instances", "restore-backup", instanceName, backupName);
    }

    /// <inheritdoc/>
    public KgsmResult GenerateId(string blueprintName, string? customName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintName, nameof(blueprintName));

        var args = new List<string> { "instances", "generate-id", blueprintName };

        if (!string.IsNullOrWhiteSpace(customName))
        {
            args.Add("--name");
            args.Add(customName);
        }

        return _commandExecutor.Execute(args.ToArray());
    }

    /// <inheritdoc/>
    public KgsmResult Save(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "save", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult SendInput(string instanceName, string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        return _commandExecutor.Execute("instances", "input", instanceName, command);
    }

    /// <inheritdoc/>
    public KgsmResult FindConfigPath(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "find", instanceName);
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
