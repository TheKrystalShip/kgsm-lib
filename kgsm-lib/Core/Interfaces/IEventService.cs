using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for handling KGSM events.
/// </summary>
public interface IEventService : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Initializes the event listener and starts listening for events, from the start
    /// position the options configured.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Initializes the event listener at an explicit start position, overriding the
    /// configured one.
    /// </summary>
    /// <param name="startPosition">Where to begin reading.</param>
    /// <remarks>
    /// Start position is a property of a replayable transport. The socket transport delivers
    /// only what arrives while it is listening and has no history to position within, so it
    /// ignores this and logs that it did — the setting is never silently honoured as
    /// something it is not.
    /// </remarks>
    void Initialize(EventStartPosition startPosition);

    /// <summary>
    /// Registers a handler for a specific event type.
    /// </summary>
    /// <typeparam name="T">The type of event data to handle — any event payload, whatever its subject
    /// (an instance, a blueprint, …).</typeparam>
    /// <param name="handler">The handler function to invoke when the event is received.</param>
    void RegisterHandler<T>(Func<T, Task> handler) where T : KgsmEventDataBase;

    /// <summary>
    /// Registers a handler that receives the full <see cref="EventWrapper"/> envelope
    /// for every event the socket delivers — including event types with no
    /// <see cref="RegisterHandler{T}"/> mapping (e.g. a new engine event a caller has
    /// no model for yet). Fires independently of, and before, typed dispatch; it never
    /// suppresses a typed handler. A raw handler that throws is caught and logged per
    /// invocation, so it can never stop the socket read loop or block other handlers
    /// (raw or typed). Multiple raw handlers may be registered; each runs for every
    /// envelope.
    /// </summary>
    /// <param name="handler">The handler function to invoke with the full envelope.</param>
    void RegisterRawHandler(Func<EventWrapper, Task> handler);

    /// <summary>
    /// Registers a handler invoked when the transport cannot resume where this consumer left
    /// off, so some events happened that it will never receive. A consumer that persists what
    /// it reads should record the gap, so it can report its history as incomplete before that
    /// point rather than implying coverage it does not have.
    /// </summary>
    /// <param name="handler">The handler function to invoke with the gap report.</param>
    /// <remarks>
    /// Only a replayable transport can detect a gap. Under the socket transport a missed event
    /// is indistinguishable from an event that never happened, so a handler registered there
    /// never fires.
    /// </remarks>
    void RegisterGapHandler(Func<EventJournalGap, Task> handler);
}
