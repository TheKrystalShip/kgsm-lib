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
    /// <typeparam name="T">The type of event data to handle.</typeparam>
    /// <param name="handler">The handler function to invoke when the event is received.</param>
    void RegisterHandler<T>(Func<T, Task> handler) where T : EventDataBase;
}
