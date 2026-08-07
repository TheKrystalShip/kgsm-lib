using System.Text.Json;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// A windowed, filtered read of the engine's event journal. Every filter is optional; an
/// unset one places no constraint.
/// </summary>
/// <remarks>
/// <see cref="Instance"/> and <see cref="Blueprint"/> are orthogonal, never one backing the
/// other: an instance-scoped event's subject is an <c>InstanceName</c> and a blueprint-scoped
/// event's is a <c>BlueprintName</c>. A query for one never returns the other, even when a
/// server and the blueprint it was built from share a name.
/// </remarks>
public sealed record EventHistoryQuery
{
    /// <summary>Default page size, used when <see cref="Limit"/> is non-positive.</summary>
    public const int DefaultLimit = 200;

    /// <summary>Hard cap — a larger <see cref="Limit"/> is clamped down to this.</summary>
    public const int MaxLimit = 1000;

    /// <summary>Scope to one instance's events, read from the payload's <c>InstanceName</c>.</summary>
    public string? Instance { get; init; }

    /// <summary>Scope to one blueprint's events, read from the payload's <c>BlueprintName</c>.</summary>
    public string? Blueprint { get; init; }

    /// <summary>Scope to one raw engine event type, e.g. <c>instance_started</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Only events at or after this unix-millisecond timestamp.</summary>
    public long? SinceMs { get; init; }

    /// <summary>Only events at or before this unix-millisecond timestamp.</summary>
    public long? UntilMs { get; init; }

    /// <summary>
    /// An <see cref="EventHistoryEntry.Id"/> to page from — the result holds only events that
    /// sort strictly after it in the returned order. Null starts at the newest event.
    /// </summary>
    public string? Before { get; init; }

    /// <summary>The maximum number of events to return. Clamped to [1, <see cref="MaxLimit"/>].</summary>
    public int Limit { get; init; } = DefaultLimit;
}

/// <summary>
/// One page of engine events, newest first.
/// </summary>
/// <param name="Events">
/// The events, ordered by timestamp descending with <see cref="EventHistoryEntry.Id"/> as the
/// tie-break. Empty is an honest "nothing matched", never a failure in disguise —
/// <paramref name="JournalReadable"/> is what distinguishes the two.
/// </param>
/// <param name="NextCursor">
/// The <see cref="EventHistoryQuery.Before"/> value for the following page, set only when this
/// page came back full. A partial page means there is nothing more to read, so the cursor is
/// honestly null rather than pointing at an empty result.
/// </param>
/// <param name="CoverageFrom">
/// The oldest moment this journal can still answer for — the timestamp of the first event in
/// the oldest surviving segment. A query whose window reaches earlier is answered only from
/// here, and saying so is what stops a partial history from reading as a complete one.
/// Retention deletes whole segments oldest-first, so this is exact: the journal has no interior
/// holes, and a date with no segment means nothing happened that day rather than that something
/// was lost. Null when the journal holds no events at all.
/// </param>
/// <param name="Truncated">
/// True when the scan stopped at its byte budget before exhausting the query's window. The page
/// is then a valid prefix of the answer rather than the whole of it — reported so a caller never
/// presents a bounded scan as an exhaustive one.
/// </param>
/// <param name="JournalReadable">
/// False when the journal directory is absent or cannot be read — the honest "no history
/// available" signal, distinct from a readable journal that matched nothing.
/// </param>
public sealed record EventHistoryPage(
    IReadOnlyList<EventHistoryEntry> Events,
    string? NextCursor,
    DateTimeOffset? CoverageFrom,
    bool Truncated,
    bool JournalReadable)
{
    /// <summary>A readable journal that matched nothing.</summary>
    public static EventHistoryPage Empty(DateTimeOffset? coverageFrom) =>
        new([], null, coverageFrom, false, true);

    /// <summary>An absent or unreadable journal.</summary>
    public static readonly EventHistoryPage Unreadable = new([], null, null, false, false);
}

/// <summary>
/// One engine event, exactly as the engine wrote it.
/// </summary>
/// <remarks>
/// Raw and neutral: no dotted action vocabulary, no severity, no human summary. Those are a
/// domain-aware reader's concern, and applying them here would force one consumer's vocabulary
/// on every other. A field the engine did not supply is null — never fabricated.
/// </remarks>
/// <param name="Id">
/// The event's position in the journal, as <c>evt_&lt;segment&gt;_&lt;offset&gt;</c>. Unique by
/// construction and ordered like the file itself, so it serves as both identity and cursor.
/// See <see cref="TheKrystalShip.KGSM.Events.AuditId.ForPosition"/>.
/// </param>
/// <param name="Ts">When the engine emitted the event.</param>
/// <param name="Type">The raw engine event type, e.g. <c>instance_started</c>.</param>
/// <param name="Instance">The instance the event is about, or null for host, global and blueprint-scoped events.</param>
/// <param name="Blueprint">The blueprint the event is about, or null for every instance-scoped event.</param>
/// <param name="Actor">Who caused it, or null when the emitter supplied no enrichment.</param>
/// <param name="Origin">The surface it came through, or null. Independent of <paramref name="Actor"/>.</param>
/// <param name="Hostname">The host that emitted it, or null.</param>
/// <param name="Data">The event-specific payload, relayed verbatim and uninterpreted.</param>
public sealed record EventHistoryEntry(
    string Id,
    DateTimeOffset Ts,
    string Type,
    string? Instance,
    string? Blueprint,
    string? Actor,
    string? Origin,
    string? Hostname,
    JsonElement? Data);
