namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// An <see cref="IEventSource"/> that receives events over a Unix domain socket it binds and
/// the engine connects to per event.
/// </summary>
/// <remarks>
/// Binding is exclusive: one process per path. That is why every consumer needs a socket path
/// of its own and why the engine must be configured with the list of them — a constraint the
/// journal transport (<see cref="IEventJournalReader"/>) does not have.
/// </remarks>
public interface IUnixSocketClient : IEventSource
{
}
