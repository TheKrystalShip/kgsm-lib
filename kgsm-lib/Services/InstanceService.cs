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
    public Dictionary<string, Reading<InstanceRuntimeStatus>> GetAllStatuses(bool fast = false)
    {
        string[] args = fast
            ? ["instances", "list", "--status", "--json", "--fast"]
            : ["instances", "list", "--status", "--json"];

        return _commandExecutor.ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(args) ?? [];
    }

    /// <inheritdoc/>
    public KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null, string? actor = null, string? origin = null)
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

        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute(_timeouts.Install, args.ToArray())
            : _commandExecutor.Execute(provenance, _timeouts.Install, args.ToArray());
    }

    /// <inheritdoc/>
    public KgsmResult Uninstall(string instanceName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        // Always --force: a programmatic uninstall through the library is, by definition, already
        // confirmed at the calling surface (e.g. the API's operator gate + the SPA's confirm flow). kgsm's
        // uninstall is interactive by default and returns a non-zero EC_CANCELLED with no TTY — so without
        // --force this would silently no-op. The destructive confirmation belongs at the product surface,
        // never as a TTY prompt to a non-interactive engine call.
        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute(_timeouts.Uninstall, "uninstall", instanceName, "--force")
            : _commandExecutor.Execute(provenance, _timeouts.Uninstall, "uninstall", instanceName, "--force");
    }

    /// <inheritdoc/>
    public ICollection<string> GetLogs(string instanceName, int lines = 10)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _lifecycleService.GetLogs(instanceName, lines);
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetLogsAsync(string instanceName, int lines = 10, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return await _lifecycleService.GetLogsAsync(instanceName, lines, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

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
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _lifecycleService.IsActive(instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult Start(string instanceName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _lifecycleService.Start(instanceName, actor, origin);
    }

    /// <inheritdoc/>
    public KgsmResult Stop(string instanceName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _lifecycleService.Stop(instanceName, actor, origin);
    }

    /// <inheritdoc/>
    public KgsmResult Restart(string instanceName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _lifecycleService.Restart(instanceName, actor, origin);
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
    public KgsmResult Update(string instanceName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute(_timeouts.Update, "instances", "update", instanceName)
            : _commandExecutor.Execute(provenance, _timeouts.Update, "instances", "update", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetBackups(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "backups", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult CreateBackup(string instanceName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute(_timeouts.Backup, "instances", "create-backup", instanceName)
            : _commandExecutor.Execute(provenance, _timeouts.Backup, "instances", "create-backup", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult RestoreBackup(string instanceName, string backupName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));
        ArgumentNullException.ThrowIfNull(backupName, nameof(backupName));

        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute(_timeouts.Restore, "instances", "restore-backup", instanceName, backupName)
            : _commandExecutor.Execute(provenance, _timeouts.Restore, "instances", "restore-backup", instanceName, backupName);
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
    public KgsmResult SendInput(string instanceName, string command, string? actor = null, string? origin = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        // input is a quick command, so the default-timeout env overload carries provenance
        // onto the instance_input_sent event kgsm emits (same pattern as SetInstanceConfigValue).
        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute("instances", "input", instanceName, command)
            : _commandExecutor.Execute(provenance, "instances", "input", instanceName, command);
    }

    /// <inheritdoc/>
    public KgsmResult FindConfigPath(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("instances", "find", instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetInstanceConfigValue(string instanceName, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        return _commandExecutor.Execute("instances", "config-get", instanceName, key);
    }

    /// <inheritdoc/>
    public KgsmResult SetInstanceConfigValue(string instanceName, string key, string value, string? actor = null, string? origin = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        // The value may legitimately be the empty string (e.g. clearing
        // executable_arguments), so only null is rejected.
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        // The whole assignment rides as a single argv element; kgsm splits it on
        // the first '=' only, so a value containing '=' is preserved. config-set is a
        // quick command, so the default-timeout env overload carries provenance.
        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute("instances", "config-set", instanceName, $"{key}={value}")
            : _commandExecutor.Execute(provenance, "instances", "config-set", instanceName, $"{key}={value}");
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
