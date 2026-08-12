using System.Text.Json;

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
    /// The event type, underscore-separated (<c>instance_ready</c>). A producer must not write a
    /// type another producer owns.
    /// </param>
    /// <param name="data">
    /// The event-specific payload. Written verbatim and compact; a payload that is not a JSON object
    /// is rejected, since every reader keys the subject off a property of it.
    /// </param>
    /// <param name="actor">Who triggered it (<c>provider:name</c>), or null when unknown.</param>
    /// <param name="origin">The surface that drove it, or null. Never fabricated.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>True when the line was appended; false when it could not be.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventType"/> is blank or <paramref name="data"/> is not a JSON
    /// object.
    /// </exception>
    ValueTask<bool> AppendAsync(
        string eventType,
        JsonElement data,
        string? actor = null,
        string? origin = null,
        CancellationToken token = default);
}
