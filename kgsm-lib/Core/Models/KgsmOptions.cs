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
    /// Gets or sets the directory holding the engine's event journal segments — where this
    /// consumer reads events from. A well-known host location rather than a per-consumer one:
    /// the engine appends and holds no list of readers, and a file has no exclusive binding, so
    /// every consumer on a host reads the same directory with no coordination between them.
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
    /// The scan budget one history query may read, used when <see cref="EventHistoryScanBudgetBytes"/>
    /// is not set. Generous against a journal that measures in single-digit megabytes over a
    /// full retention window, and low enough that a pathological query cannot read for minutes.
    /// </summary>
    public const long DefaultEventHistoryScanBudgetBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most bytes a single <see cref="Interfaces.IEventJournalHistory"/> query
    /// may read. A query that reaches the budget returns what it found and reports itself
    /// truncated, rather than reading unbounded or silently answering from part of the window.
    /// </summary>
    public long EventHistoryScanBudgetBytes { get; set; } = DefaultEventHistoryScanBudgetBytes;

    /// <summary>
    /// Gets or sets the per-operation process timeouts. Defaults are generous
    /// (see <see cref="KgsmTimeoutOptions"/>) so slow installs/updates aren't
    /// killed out of the box; override any tier to tune.
    /// </summary>
    public KgsmTimeoutOptions Timeouts { get; set; } = new();
}