using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the ISystemService interface for managing system-level operations via KGSM.
/// </summary>
public class SystemService : ISystemService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<SystemService> _logger;

    /// <summary>
    /// Initializes a new instance of the SystemService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public SystemService(IKgsmCommandExecutor commandExecutor, ILogger<SystemService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("SystemService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult Shutdown(int delayMinutes = 0)
    {
        if (delayMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMinutes), "Delay must be zero or a positive number of minutes.");

        return _commandExecutor.Execute("system", "shutdown", delayMinutes.ToString());
    }

    /// <inheritdoc/>
    public KgsmResult Restart(int delayMinutes = 0)
    {
        if (delayMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMinutes), "Delay must be zero or a positive number of minutes.");

        return _commandExecutor.Execute("system", "restart", delayMinutes.ToString());
    }

    /// <inheritdoc/>
    public KgsmResult CancelScheduled()
        => _commandExecutor.Execute("system", "cancel");

    /// <inheritdoc/>
    public KgsmResult GetUptime()
        => _commandExecutor.Execute("system", "uptime");

    /// <inheritdoc/>
    public KgsmResult GetLoad()
        => _commandExecutor.Execute("system", "load");

    /// <inheritdoc/>
    public KgsmResult GetMemory()
        => _commandExecutor.Execute("system", "memory");

    /// <inheritdoc/>
    public KgsmResult GetDisk()
        => _commandExecutor.Execute("system", "disk");

    /// <inheritdoc/>
    public bool IsRebootRequired()
        => _commandExecutor.Probe("system", "reboot-required").IsSuccess;

    /// <inheritdoc/>
    public KgsmResult GetInfo()
        => _commandExecutor.Execute("system", "info");

    /// <inheritdoc/>
    public T? GetInfo<T>()
        => _commandExecutor.ExecuteForJson<T>(["system", "info", "--json"]);

    /// <inheritdoc/>
    public SystemInfo? GetSystemInfo()
        => _commandExecutor.ExecuteForJson<SystemInfo>(["system", "info", "--json"]);
}
