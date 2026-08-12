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
/// <see cref="TheKrystalShip.KGSM.Events.AuditId.ForPosition(string, long)"/> turn it into a stable id.
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
    /// Which producer's journal this position is in, or <see langword="null"/> when the transport
    /// did not say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null rather than a default of <c>"kgsm"</c>: a transport that does not report a producer has
    /// not told us it was the engine, and filling that in would be exactly the fabricated
    /// provenance the location-over-field rule exists to prevent. It is also the only safe spelling
    /// for a struct — <c>default(EventPosition)</c> skips property initializers, so any non-null
    /// default here would be a lie in the default value.
    /// </para>
    /// <para>
    /// Set by a transport reading more than one journal; a single-journal reader leaves it null and
    /// its caller supplies the producer it configured.
    /// </para>
    /// </remarks>
    public string? Producer { get; init; }

    /// <summary>
    /// Initializes a position in a named producer's journal.
    /// </summary>
    /// <param name="producer">The producer id whose journal holds the event.</param>
    /// <param name="segment">The segment file name.</param>
    /// <param name="offset">The byte offset the event's line starts at.</param>
    public EventPosition(string producer, string segment, long offset)
        : this(segment, offset) => Producer = producer;

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
