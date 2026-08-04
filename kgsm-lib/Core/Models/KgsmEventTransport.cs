namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// How a consumer receives KGSM events.
/// </summary>
public enum KgsmEventTransport
{
    /// <summary>
    /// A unix socket the consumer binds and the engine connects to per event. Binding is
    /// exclusive, so each consumer needs its own path and the engine must be configured
    /// with the list of them. Delivery is live-only: an event emitted while the consumer is
    /// down is gone.
    /// </summary>
    Socket = 0,

    /// <summary>
    /// The on-disk append-only journal the engine writes. Any number of consumers read it
    /// concurrently with no coordination and no engine-side configuration, each at its own
    /// pace from its own cursor, and a consumer that was down catches up on restart.
    /// </summary>
    Journal
}
