using System.Text.Json;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Appends events to the calling component's own event journal.
/// </summary>
/// <remarks>
/// <para>
/// The write half of the journal, and the reason a component recording what it did needs nothing
/// else running: one append to a file it owns, rather than a process spawned to ask another
/// component to write it down. A resident daemon paid a full engine bootstrap per event before this
/// existed, and the line it produced named the wrong author.
/// </para>
/// <para>
/// <b>A producer writes only what that producer did.</b> This interface takes no actor override and
/// no producer parameter for exactly that reason — the producer is fixed at construction, and a
/// component that finds itself wanting to write another's event is describing something it did not
/// do.
/// </para>
/// <para>
/// <b>A line describes itself.</b> Beside the payload, a producer says how much the event matters
/// (<see cref="EventSeverity"/>), how it went (<see cref="EventOutcome"/>) and what happened in one
/// line of prose. Those three are what let a reader render an event it has never heard of, so no
/// consumer holds a list of event types and none can be missing one. All three are optional: a
/// producer that says nothing is quiet rather than malformed, and a reader treats absence as
/// unknown.
/// </para>
/// <para>
/// Writes are best-effort in the same sense the engine's are: a failure is reported to the caller
/// and logged, never thrown into an operation that has already happened. An unrecorded action that
/// looks recorded is the failure mode the journal exists to prevent, so the caller is told — and
/// deciding what to do about it stays the caller's.
/// </para>
/// </remarks>
public interface IEventJournalWriter
{
    /// <summary>The producer id this writer stamps by writing to its own journal.</summary>
    string Producer { get; }

    /// <summary>
    /// Appends one event.
    /// </summary>
    /// <param name="eventType">
    /// The event's name. A producer must not write a name another producer owns.
    /// </param>
    /// <param name="data">
    /// The event-specific payload. Written verbatim and compact; a payload that is not a JSON object
    /// is rejected, since every reader keys the subject off a property of it.
    /// </param>
    /// <param name="actor">Who triggered it (<c>provider:name</c>), or null when unknown.</param>
    /// <param name="origin">The surface that drove it, or null. Never fabricated.</param>
    /// <param name="severity">How much it matters, or null when the producer does not say.</param>
    /// <param name="outcome">How it went, or null when the producer does not say.</param>
    /// <param name="summary">
    /// What happened, in one line, for a person to read. Written at emit time, so it names things as
    /// they were called when it happened rather than as they are called now.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>True when the line was appended; false when it could not be.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventType"/> names nothing or <paramref name="data"/> is not a
    /// JSON object.
    /// </exception>
    ValueTask<bool> AppendAsync(
        EventName eventType,
        JsonElement data,
        string? actor = null,
        string? origin = null,
        EventSeverity? severity = null,
        EventOutcome? outcome = null,
        string? summary = null,
        CancellationToken token = default);

    /// <summary>
    /// Appends one event, writing its payload directly.
    /// </summary>
    /// <remarks>
    /// The overload a producer actually wants. A component holds typed values, not a
    /// <see cref="JsonElement"/>, and the two ways of bridging that gap are both worse: composing JSON
    /// by string concatenation puts an escaping bug one unusual instance name away, and serializing a
    /// payload model needs a registered type per event in a library that must stay reflection-free.
    /// Writing the properties straight out is neither.
    /// <para>
    /// <paramref name="writeData"/> is called with the writer positioned inside the payload object, so
    /// it writes properties only — no <c>WriteStartObject</c>/<c>WriteEndObject</c> of its own.
    /// </para>
    /// </remarks>
    /// <param name="eventType">The event's name.</param>
    /// <param name="writeData">Writes the payload's properties.</param>
    /// <param name="actor">Who triggered it (<c>provider:name</c>), or null when unknown.</param>
    /// <param name="origin">The surface that drove it, or null. Never fabricated.</param>
    /// <param name="severity">How much it matters, or null when the producer does not say.</param>
    /// <param name="outcome">How it went, or null when the producer does not say.</param>
    /// <param name="summary">What happened, in one line, for a person to read.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>True when the line was appended; false when it could not be.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writeData"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> names nothing.</exception>
    ValueTask<bool> AppendAsync(
        EventName eventType,
        Action<Utf8JsonWriter> writeData,
        string? actor = null,
        string? origin = null,
        EventSeverity? severity = null,
        EventOutcome? outcome = null,
        string? summary = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(writeData, nameof(writeData));

        var buffer = new System.Buffers.ArrayBufferWriter<byte>(256);

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writeData(writer);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);

        // Cloned because the document is disposed on return and a JsonElement does not own its buffer.
        return AppendAsync(
            eventType, document.RootElement.Clone(), actor, origin, severity, outcome, summary, token);
    }
}
