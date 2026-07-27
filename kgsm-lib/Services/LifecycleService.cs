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
    private readonly KgsmTimeoutOptions _timeouts;
    private readonly ILogger<LifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the LifecycleService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="kgsmOptions">
    /// KGSM options, used here for the per-operation timeouts. Optional: when null
    /// (e.g. in tests that don't exercise timeouts), generous defaults are used.
    /// The DI container injects the registered instance.
    /// </param>
    public LifecycleService(
        IKgsmCommandExecutor commandExecutor,
        ILogger<LifecycleService> logger,
        KgsmOptions? kgsmOptions = null)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _timeouts = kgsmOptions?.Timeouts ?? new KgsmTimeoutOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("LifecycleService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult Start(string instanceName, string? actor = null, string? origin = null)
        => RunLifecycle("start", instanceName, actor, origin);

    /// <inheritdoc/>
    public KgsmResult Stop(string instanceName, string? actor = null, string? origin = null)
        => RunLifecycle("stop", instanceName, actor, origin);

    /// <inheritdoc/>
    public KgsmResult Restart(string instanceName, string? actor = null, string? origin = null)
        => RunLifecycle("restart", instanceName, actor, origin);

    /// <summary>
    /// Runs a lifecycle verb, propagating any supplied provenance (actor/origin) to KGSM
    /// as environment variables so the event it emits is attributable. When neither is
    /// supplied, the plain (no-env) command path is used so KGSM applies its own honest
    /// fallbacks (actor → OS user; origin → none).
    /// <para>
    /// Runs on the <see cref="KgsmTimeoutOptions.Lifecycle"/> tier, not the default one: a stop drains
    /// for the instance's full stop timeout before its supervisor hard-kills, which the 30s default
    /// cannot cover. Timing out here kills the KGSM process tree mid-stop and reports a failure for work
    /// that completes regardless — the caller learns nothing true.
    /// </para>
    /// </summary>
    private KgsmResult RunLifecycle(string verb, string instanceName, string? actor, string? origin)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);

        return provenance is null
            ? _commandExecutor.Execute(_timeouts.Lifecycle, "lifecycle", verb, instanceName)
            : _commandExecutor.Execute(provenance, _timeouts.Lifecycle, "lifecycle", verb, instanceName);
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return _commandExecutor.Execute("lifecycle", "status", instanceName);
    }

    /// <inheritdoc/>
    public bool IsActive(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        // 'is-active' signals state via exit code (0 = active, non-zero = inactive),
        // so a non-zero result is a normal outcome, not an error.
        return _commandExecutor.Probe("lifecycle", "is-active", instanceName).IsSuccess;
    }

    /// <inheritdoc/>
    public ICollection<string> GetLogs(string instanceName, int lines = 10)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        KgsmResult result = _commandExecutor.Execute("lifecycle", "logs", instanceName, "--tail", lines.ToString());

        if (!result.IsSuccess)
            throw new InvalidOperationException($"Failed to get logs for instance '{instanceName}': {result.Stderr}");

        return result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <inheritdoc/>
    public async Task<ICollection<string>> GetLogsAsync(string instanceName, int lines = 10, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        KgsmResult result = await _commandExecutor.ExecuteAsync(["lifecycle", "logs", instanceName, "--tail", lines.ToString()], cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
            throw new InvalidOperationException($"Failed to get logs for instance '{instanceName}': {result.Stderr}");

        return result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
