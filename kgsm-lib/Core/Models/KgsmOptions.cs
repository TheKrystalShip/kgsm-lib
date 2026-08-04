namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Options for configuring KGSM services.
/// </summary>
public class KgsmOptions
{
    /// <summary>
    /// The journal directory KGSM writes events to on a standard install, used when
    /// <see cref="EventJournalDirectory"/> is not set. It is a well-known host path rather
    /// than a per-consumer one, because any number of consumers read the same journal.
    /// </summary>
    public const string DefaultEventJournalDirectory = "/var/lib/kgsm/events";

    /// <summary>
    /// Gets or sets the path to the KGSM executable.
    /// </summary>
    public string KgsmPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the KGSM Unix socket. Required only when
    /// <see cref="EventTransport"/> is <see cref="KgsmEventTransport.Socket"/>.
    /// </summary>
    public string SocketPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how events reach this consumer. Defaults to
    /// <see cref="KgsmEventTransport.Socket"/>, so taking a new version of the library never
    /// changes an existing consumer's transport on its own — moving to the journal is an
    /// explicit choice each consumer makes and verifies.
    /// </summary>
    public KgsmEventTransport EventTransport { get; set; } = KgsmEventTransport.Socket;

    /// <summary>
    /// Gets or sets the directory holding the engine's event journal segments. Used when
    /// <see cref="EventTransport"/> is <see cref="KgsmEventTransport.Journal"/>.
    /// </summary>
    public string EventJournalDirectory { get; set; } = DefaultEventJournalDirectory;

    /// <summary>
    /// Gets or sets where this consumer's journal position is stored between runs. Leave it
    /// unset to keep no position at all — every run then starts from
    /// <see cref="EventStartPosition"/>'s cold-start behaviour. Ignored by a consumer that
    /// registers its own <see cref="Interfaces.IEventCursorStore"/>.
    /// </summary>
    public string? EventCursorPath { get; set; }

    /// <summary>
    /// Gets or sets where journal reading begins. The right answer differs per consumer: one
    /// that indexes events must be able to replay them, while one that announces them must
    /// never replay a backlog into a chat channel.
    /// </summary>
    public EventStartPosition EventStartPosition { get; set; } = EventStartPosition.CursorOrTail;

    /// <summary>
    /// Gets or sets the per-operation process timeouts. Defaults are generous
    /// (see <see cref="KgsmTimeoutOptions"/>) so slow installs/updates aren't
    /// killed out of the box; override any tier to tune.
    /// </summary>
    public KgsmTimeoutOptions Timeouts { get; set; } = new();
}