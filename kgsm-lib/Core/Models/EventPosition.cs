namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Where an event sits in the journal — the segment holding it and the byte offset its line
/// begins at.
/// </summary>
/// <remarks>
/// <para>
/// This is the event's identity. Because each event is one whole line and retention deletes
/// whole segments rather than rewriting them, no two events ever share a position and an
/// event's position never changes — which is what lets
/// <see cref="TheKrystalShip.KGSM.Events.AuditId.ForPosition"/> turn it into a stable id.
/// </para>
/// <para>
/// A consumer watching events arrive and a consumer reading history back therefore name the
/// same event the same way, with no coordination: one gets the position from the transport,
/// the other from the file, and both compute the same id.
/// </para>
/// </remarks>
/// <param name="Segment">The segment file name, e.g. <c>2026-08-07.ndjson</c>.</param>
/// <param name="Offset">The byte offset the event's line starts at.</param>
public readonly record struct EventPosition(string Segment, long Offset)
{
    /// <summary>
    /// The absence of a position, for a transport that cannot supply one. Consumers that key on
    /// position must treat it as "not addressable" rather than as a real location — it names no
    /// event, and every occurrence of it is equal to every other.
    /// </summary>
    public static readonly EventPosition None = new(string.Empty, 0);

    /// <summary>Whether this names a real journal position.</summary>
    public bool IsKnown => !string.IsNullOrEmpty(Segment);

    /// <inheritdoc/>
    public override string ToString() => IsKnown ? $"{Segment}+{Offset}" : "(no position)";
}
