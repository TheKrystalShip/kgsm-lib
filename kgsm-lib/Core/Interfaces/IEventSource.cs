namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// A transport that delivers raw KGSM event envelopes, one JSON document per message.
/// <see cref="Services.EventService"/> deserializes and dispatches whatever arrives here,
/// so a transport's whole job is producing lines — it never inspects them.
/// </summary>
public interface IEventSource : IDisposable
{
    /// <summary>
    /// Raised once per event envelope received, with the raw JSON. A handler that throws is
    /// logged and swallowed by the transport: one unparseable or mishandled event never
    /// stops the stream.
    /// </summary>
    event Func<string, Task>? EventReceived;

    /// <summary>
    /// Runs the transport until <paramref name="token"/> is cancelled. Long-running: callers
    /// start it on a background task rather than awaiting it inline.
    /// </summary>
    /// <param name="token">Cancellation token that stops the transport.</param>
    /// <returns>A task that completes when the transport has stopped.</returns>
    Task StartListeningAsync(CancellationToken token);
}
