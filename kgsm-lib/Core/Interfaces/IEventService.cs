using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for handling KGSM events.
/// </summary>
public interface IEventService : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Initializes the event listener and starts listening for events.
    /// </summary>
    void Initialize();

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
}
