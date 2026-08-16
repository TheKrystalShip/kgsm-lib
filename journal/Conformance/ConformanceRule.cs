namespace TheKrystalShip.KGSM.Conformance;

/// <summary>
/// The rules a conforming journal line satisfies, each named so a finding says which one broke.
/// </summary>
/// <remarks>
/// <para>
/// Every rule here restates something the envelope contract already says. None of them is a new
/// requirement, and none of them is policy: what a producer chooses to record, and when, is its own
/// business, and no rule in this class looks at an event type or a payload field.
/// </para>
/// <para>
/// The ids are stable strings rather than an enum because a finding is something an operator reads
/// and greps for, and a rule dropped later would otherwise renumber the ones after it.
/// </para>
/// </remarks>
public static class ConformanceRule
{
    /// <summary>The line is one JSON object.</summary>
    public const string LineParses = "line.parses";

    /// <summary>The envelope declares a schema version this reader understands.</summary>
    public const string SchemaVersion = "envelope.schema-version";

    /// <summary>The event type is present and spelled the way the wire spells it.</summary>
    public const string EventType = "envelope.event-type";

    /// <summary>The payload is present and is a JSON object.</summary>
    public const string Data = "envelope.data";

    /// <summary>The timestamp is millisecond-precision UTC.</summary>
    public const string Timestamp = "envelope.timestamp";

    /// <summary>
    /// An absent value is spelled as absence, not as an empty string.
    /// </summary>
    /// <remarks>
    /// Absent and <c>null</c> are the same thing to a reader. An empty string is a third state the
    /// contract does not define — neither a value nor the absence of one — and a reader checking for
    /// null does not find it.
    /// </remarks>
    public const string AbsentSpelling = "envelope.absent-spelling";

    /// <summary>An actor, when there is one, names somebody.</summary>
    public const string Actor = "envelope.actor";

    /// <summary>The producer version is the number a release carries, not a four-part assembly version.</summary>
    public const string ProducerVersionShape = "envelope.producer-version-shape";

    /// <summary>The envelope carries no field the contract does not define.</summary>
    public const string UnknownField = "envelope.unknown-field";

    /// <summary>A reader attributes lines in this directory to the producer that writes them.</summary>
    public const string ProducerMatchesDirectory = "journal.producer-matches-directory";

    /// <summary>A segment is named for the day it holds.</summary>
    public const string SegmentName = "journal.segment-name";

    /// <summary>The journal has something in it to check.</summary>
    public const string JournalReadable = "journal.readable";

    /// <summary>Every producer on one host agrees about which host it is.</summary>
    public const string HostnameAgrees = "host.hostname-agrees";
}
