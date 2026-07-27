namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// An <see cref="IEventManagementService"/> that records what was emitted instead of shelling out to
/// kgsm, so a test can assert the event type, provenance, and parameters an authority actually threaded
/// through. Only <see cref="EmitWithProvenance"/> and <see cref="Emit"/> are exercised; every other
/// member is a no-op success, since nothing under test configures transports.
/// </summary>
public sealed class RecordingEventManagementService : IEventManagementService
{
    /// <summary>One recorded emission.</summary>
    /// <param name="EventType">The event type as passed to the engine (dash-separated).</param>
    /// <param name="Actor">The actor threaded through, or null when the caller supplied none.</param>
    /// <param name="Origin">The origin threaded through, or null when the caller supplied none.</param>
    /// <param name="Parameters">The positional event parameters.</param>
    public sealed record Emission(string EventType, string? Actor, string? Origin, string[] Parameters);

    private readonly List<Emission> _emissions = [];

    /// <summary>Every emission in order.</summary>
    public IReadOnlyList<Emission> Emissions => _emissions;

    /// <summary>The exit code every emit reports — set non-zero to model a failing engine.</summary>
    public int ExitCode { get; set; }

    /// <summary>When set, every emit throws it — models the engine being unreachable entirely.</summary>
    public Exception? Throws { get; set; }

    /// <summary>The single recorded emission; fails the assertion if there was not exactly one.</summary>
    public Emission Single() => Assert.Single(_emissions);

    /// <inheritdoc/>
    public KgsmResult EmitWithProvenance(string eventType, string? actor, string? origin, params string[] parameters)
    {
        if (Throws is not null) throw Throws;
        _emissions.Add(new Emission(eventType, actor, origin, parameters));
        return new KgsmResult(ExitCode);
    }

    /// <inheritdoc/>
    public KgsmResult Emit(string eventType, params string[] parameters) =>
        EmitWithProvenance(eventType, null, null, parameters);

    /// <inheritdoc/>
    public KgsmResult GetStatus() => new(0);

    /// <inheritdoc/>
    public KgsmResult TestTransport(string transport) => new(0);

    /// <inheritdoc/>
    public KgsmResult EnableSocket() => new(0);

    /// <inheritdoc/>
    public KgsmResult DisableSocket() => new(0);

    /// <inheritdoc/>
    public KgsmResult TestSocket() => new(0);

    /// <inheritdoc/>
    public KgsmResult GetSocketStatus() => new(0);

    /// <inheritdoc/>
    public KgsmResult EnableWebhook() => new(0);

    /// <inheritdoc/>
    public KgsmResult DisableWebhook() => new(0);

    /// <inheritdoc/>
    public KgsmResult TestWebhook() => new(0);

    /// <inheritdoc/>
    public KgsmResult GetWebhookStatus() => new(0);
}
