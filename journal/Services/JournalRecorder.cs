using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// The one write path a producer records through.
/// </summary>
/// <remarks>
/// <para>
/// A producer's journal has two halves. <b>What it says</b> — its event types and the shape of each
/// payload — is its own vocabulary and belongs in its own repo; a derived class writes exactly that
/// and nothing else. <b>How a line gets written</b> — normalising the type, defaulting the actor,
/// deciding what happens when the write fails, spelling an absent value — is the same for every
/// producer, and is here, because these are the parts that drift when each producer answers them.
/// </para>
/// <para>
/// <b>Recording never fails an operation.</b> The action the line describes has already happened by
/// the time this is called, so refusing it because the record could not be written would trade a
/// missing line for broken behaviour. Every failure is logged and reported back; nothing is thrown.
/// </para>
/// <para>
/// <b>A failed write is never silent.</b> The writer logs why it could not write; this logs what was
/// lost, which the writer cannot know — an action that happened and will not be findable afterwards.
/// An unrecorded action that looks recorded is the failure mode the journal exists to prevent.
/// </para>
/// </remarks>
/// <param name="writer">The producer's own journal writer.</param>
/// <param name="logger">The derived recorder's logger.</param>
public abstract class JournalRecorder(IEventJournalWriter writer, ILogger logger)
{
    /// <summary>The origin of an action no product surface drove.</summary>
    public const string OriginSystem = "system";

    private readonly IEventJournalWriter _writer =
        writer ?? throw new ArgumentNullException(nameof(writer));

    private readonly ILogger _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Who this producer is when it writes.</summary>
    protected string Producer => _writer.Producer;

    /// <summary>
    /// The actor a record carries when the call site names none.
    /// </summary>
    /// <remarks>
    /// Derived from the producer id, so a component's identity has one source rather than a constant
    /// beside it free to disagree. ⚠ A producer whose events are mostly driven by <em>people</em>
    /// overrides this to null: attributing an action with an unknown actor to the daemon that carried
    /// it out states an author it does not know, and an honest null is what the envelope is for.
    /// </remarks>
    protected virtual string? DefaultActor => JournalProducer.SystemActorFor(Producer);

    /// <summary>
    /// The origin a record carries when the call site names none.
    /// </summary>
    /// <remarks>
    /// ⚠ Origin is never fabricated. This defaults to <see cref="OriginSystem"/> because a producer
    /// acting on its own really was driven by no product surface — that is a fact, not a placeholder.
    /// A producer acting on somebody's behalf passes the surface that drove it, or overrides this to
    /// null when it cannot know.
    /// </remarks>
    protected virtual string? DefaultOrigin => OriginSystem;

    /// <summary>
    /// Appends one event.
    /// </summary>
    /// <param name="eventType">
    /// The event type. Dashes are normalised to underscores, so a call site may name an event the way
    /// the engine's command line does.
    /// </param>
    /// <param name="payload">
    /// Writes the payload's properties. Called with the writer positioned inside the payload object,
    /// so it writes properties only.
    /// </param>
    /// <param name="actor">Who triggered it. Null uses <see cref="DefaultActor"/>.</param>
    /// <param name="origin">The surface that drove it. Null uses <see cref="DefaultOrigin"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the line was appended; false when it was not.</returns>
    protected async Task<bool> RecordAsync(
        string eventType,
        Action<Utf8JsonWriter> payload,
        string? actor = null,
        string? origin = null,
        CancellationToken ct = default)
    {
        string type = NormalizeType(eventType);

        try
        {
            bool written = await _writer
                .AppendAsync(type, payload, Resolve(actor, DefaultActor), Resolve(origin, DefaultOrigin), ct)
                .ConfigureAwait(false);

            if (!written)
            {
                _logger.LogWarning(
                    "{Event} was NOT recorded — it happened, the record of it did not", type);
            }

            return written;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Event} could not be recorded (event dropped)", type);
            return false;
        }
    }

    /// <summary>
    /// Appends one event, in place.
    /// </summary>
    /// <remarks>
    /// For a producer whose ordering is the point. An append is a single write to a file this process
    /// owns, so it is fast enough to do inline — and doing it inline is what makes the journal's order
    /// the order things happened, where hopping to the thread pool lets two events emitted back to
    /// back land the wrong way round.
    /// <para>
    /// ⚠ Blocking on the append is only reasonable because it is one file write. A producer that
    /// records from an async context should await <see cref="RecordAsync"/> instead.
    /// </para>
    /// </remarks>
    /// <param name="eventType">The event type; dashes normalised to underscores.</param>
    /// <param name="payload">Writes the payload's properties.</param>
    /// <param name="actor">Who triggered it. Null uses <see cref="DefaultActor"/>.</param>
    /// <param name="origin">The surface that drove it. Null uses <see cref="DefaultOrigin"/>.</param>
    /// <returns>True when the line was appended; false when it was not.</returns>
    protected bool Record(
        string eventType,
        Action<Utf8JsonWriter> payload,
        string? actor = null,
        string? origin = null)
        => RecordAsync(eventType, payload, actor, origin).GetAwaiter().GetResult();

    /// <summary>
    /// Writes a value, or a real JSON null when there is none — never an empty string.
    /// </summary>
    /// <remarks>
    /// An empty string is a third state the envelope does not define: it is neither a value nor the
    /// absence of one, and a reader checking for null does not find it.
    /// </remarks>
    /// <param name="writer">The payload writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value, or null.</param>
    protected static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        if (string.IsNullOrEmpty(value))
            writer.WriteNull(name);
        else
            writer.WriteString(name, value);
    }

    /// <summary>Null for a value that is blank, so an envelope field is omitted rather than empty.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>The value, or null when it is blank.</returns>
    protected static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// The event type as the wire spells it.
    /// </summary>
    /// <remarks>
    /// Dash on a command line, underscore on the wire. Applied here so a call site naming an event the
    /// engine's way cannot produce a type no consumer recognises.
    /// </remarks>
    private static string NormalizeType(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));
        return eventType.Replace('-', '_');
    }

    /// <summary>The caller's value, or the producer's default when the caller named none.</summary>
    private static string? Resolve(string? supplied, string? fallback) =>
        NullIfBlank(supplied) ?? NullIfBlank(fallback);
}
