using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IEventManagementService interface for managing the KGSM event system.
/// Maps CLI-side event management commands (separate from EventService, which handles
/// real-time socket event listening).
/// </summary>
public class EventManagementService : IEventManagementService
{
    private static readonly HashSet<string> ValidTransports =
        new(StringComparer.OrdinalIgnoreCase) { "all", "socket", "webhook" };

    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<EventManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of the EventManagementService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public EventManagementService(IKgsmCommandExecutor commandExecutor, ILogger<EventManagementService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("EventManagementService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus()
        => _commandExecutor.Execute("events", "status");

    /// <inheritdoc/>
    public KgsmResult TestTransport(string transport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transport, nameof(transport));

        if (!ValidTransports.Contains(transport))
        {
            throw new ArgumentException(
                $"Invalid transport '{transport}'. Must be one of: all, socket, webhook.",
                nameof(transport));
        }

        return _commandExecutor.Execute("events", "test", transport);
    }

    /// <inheritdoc/>
    public KgsmResult Emit(string eventType, params string[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));

        string[] args = ["events", "emit", eventType, .. parameters];

        return _commandExecutor.Execute(args);
    }

    /// <inheritdoc/>
    public KgsmResult EnableSocket()
        => _commandExecutor.Execute("events", "socket", "enable");

    /// <inheritdoc/>
    public KgsmResult DisableSocket()
        => _commandExecutor.Execute("events", "socket", "disable");

    /// <inheritdoc/>
    public KgsmResult TestSocket()
        => _commandExecutor.Execute("events", "socket", "test");

    /// <inheritdoc/>
    public KgsmResult GetSocketStatus()
        => _commandExecutor.Execute("events", "socket", "status");

    /// <inheritdoc/>
    public KgsmResult EnableWebhook()
        => _commandExecutor.Execute("events", "webhook", "enable");

    /// <inheritdoc/>
    public KgsmResult DisableWebhook()
        => _commandExecutor.Execute("events", "webhook", "disable");

    /// <inheritdoc/>
    public KgsmResult TestWebhook()
        => _commandExecutor.Execute("events", "webhook", "test");

    /// <inheritdoc/>
    public KgsmResult GetWebhookStatus()
        => _commandExecutor.Execute("events", "webhook", "status");
}
