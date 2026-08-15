using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Appends v1 envelopes to one producer's journal, one whole line per event.
/// </summary>
/// <remarks>
/// <para>
/// <b>One line, one write.</b> The file is opened in append mode and the entire encoded line —
/// newline included — goes out in a single write call. <c>O_APPEND</c> makes a write below
/// <c>PIPE_BUF</c> atomic, which is what lets any number of writers and readers share a segment with
/// no locking: concurrent appends interleave whole lines and never partial ones. A line longer than
/// <see cref="AtomicWriteLimitBytes"/> loses that guarantee and is logged, because a reader's cursor
/// is a byte offset and a torn line costs more than the event.
/// </para>
/// <para>
/// <b>The envelope is composed by hand</b> rather than serialized from a model. That keeps field
/// order as documented, omits nulls instead of writing them (absent and null mean the same thing to
/// a reader), and needs no new type registered for serialization — the library is reflection-free for
/// its Native-AOT consumers, so a writer that reached for a reflection-based serializer would not
/// survive publishing.
/// </para>
/// <para>
/// <b>Timestamps are millisecond-precision UTC.</b> A single appender gets ordering for free from
/// the file; several journals merged on second granularity would order arbitrarily inside each
/// second, which is exactly where causally adjacent events sit.
/// </para>
/// </remarks>
public sealed class EventJournalWriter : IEventJournalWriter
{
    /// <summary>
    /// The largest line this writer can append atomically. <c>PIPE_BUF</c> on Linux; a single
    /// <c>O_APPEND</c> write up to this size cannot interleave with another writer's.
    /// </summary>
    public const int AtomicWriteLimitBytes = 4096;

    /// <summary>The envelope schema version this writer produces.</summary>
    public const int SchemaVersion = 1;

    private readonly EventJournalWriterOptions _options;
    private readonly string _directory;
    private readonly ILogger<EventJournalWriter> _logger;

    /// <summary>
    /// Initializes a writer for the producer named in <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Which producer this is, where its journal lives, and its version.</param>
    /// <param name="logger">The logger to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the options are not usable.</exception>
    public EventJournalWriter(EventJournalWriterOptions options, ILogger<EventJournalWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        options.Validate();

        _options = options;
        // Validate() fills Directory from the producer when it was left unset, so it is non-null
        // from here on; held separately so that stays true whatever happens to the options object.
        _directory = options.Directory!;
        _logger = logger;

        // A journal a reader will not attribute to this producer is the one misconfiguration that
        // reports itself as normal operation — the writes succeed and the record is invisible. Said
        // once, at construction, because there is no later moment at which anything notices.
        if (options.DescribeDirectoryMismatch() is { } mismatch)
            _logger.LogWarning("Event journal misconfigured: {Problem}", mismatch);

        EnsureDirectory();
    }

    /// <summary>
    /// Creates the journal directory, so this producer is discoverable before it has anything to say.
    /// </summary>
    /// <remarks>
    /// A reader finds a producer by finding its directory, and a consumer scans once when it starts.
    /// Left to the first event, a deployed producer that has not yet had cause to record anything is
    /// indistinguishable from one that writes no journal at all — and stays that way until it emits
    /// <em>and</em> every consumer restarts.
    /// <para>
    /// Failure is not fatal here: the append path creates the directory too, and a permission problem
    /// reported now would be reported again with the event it actually cost.
    /// </para>
    /// </remarks>
    private void EnsureDirectory()
    {
        try
        {
            Directory.CreateDirectory(_directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex,
                "Could not create the journal directory at {Path}; this producer stays undiscoverable "
                + "until it can be written", _directory);
        }
    }

    /// <inheritdoc/>
    public string Producer => _options.Producer;

    /// <inheritdoc/>
    public ValueTask<bool> AppendAsync(
        string eventType,
        JsonElement data,
        string? actor = null,
        string? origin = null,
        CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));

        if (data.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"An event payload must be a JSON object; got {data.ValueKind}.", nameof(data));
        }

        DateTimeOffset now = _options.Clock?.Invoke() ?? DateTimeOffset.UtcNow;
        byte[] line = Compose(eventType, data, actor, origin, now);

        if (line.Length > AtomicWriteLimitBytes)
        {
            _logger.LogWarning(
                "Event '{Type}' encodes to {Bytes} bytes, past the {Limit}-byte atomic-append limit; "
                + "a concurrent write to this segment could interleave with it",
                eventType, line.Length, AtomicWriteLimitBytes);
        }

        return new ValueTask<bool>(Append(line, now, eventType, token));
    }

    /// <summary>
    /// Writes the encoded line to the segment for <paramref name="now"/>, creating the directory on
    /// first use.
    /// </summary>
    private bool Append(byte[] line, DateTimeOffset now, string eventType, CancellationToken token)
    {
        string segment = now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".ndjson";
        string path = Path.Combine(_directory, segment);

        try
        {
            token.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_directory);

            // FileMode.Append opens with O_APPEND, so the single Write below lands at the end of the
            // file as one operation even with other writers on the same segment. FileShare allows the
            // concurrent readers tailing it, and retention unlinking it underneath.
            using var stream = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 0, useAsync: false);

            stream.Write(line, 0, line.Length);
            stream.Flush();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // The action already happened; failing it now because recording it did not work would
            // trade a missing audit line for a broken operation. The caller is told and decides.
            _logger.LogError(ex,
                "Event '{Type}' was NOT recorded: the journal at {Path} could not be written",
                eventType, path);
            return false;
        }
    }

    /// <summary>
    /// Encodes one v1 envelope, compact, with a trailing newline. Null fields are omitted.
    /// </summary>
    private byte[] Compose(
        string eventType, JsonElement data, string? actor, string? origin, DateTimeOffset now)
    {
        var buffer = new ArrayBufferWriter<byte>(512);

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();

            writer.WriteNumber("V", SchemaVersion);
            writer.WriteString("EventType", eventType);

            writer.WritePropertyName("Data");
            data.WriteTo(writer);

            writer.WriteString(
                "Timestamp",
                now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));

            // Absent means null to every reader, so an unknown actor or origin is left out rather
            // than written as an explicit null — and never filled in with a plausible substitute.
            if (!string.IsNullOrEmpty(actor))
                writer.WriteString("Actor", actor);

            if (!string.IsNullOrEmpty(origin))
                writer.WriteString("Origin", origin);

            if (!string.IsNullOrEmpty(_options.Hostname))
                writer.WriteString("Hostname", _options.Hostname);

            if (!string.IsNullOrEmpty(_options.ProducerVersion))
                writer.WriteString("ProducerVersion", _options.ProducerVersion);

            // OpId / RunId / During are reserved (the correlation work) and nothing populates them
            // yet, so no producer writes them and every reader sees them absent.

            writer.WriteEndObject();
        }

        ReadOnlySpan<byte> json = buffer.WrittenSpan;
        byte[] line = new byte[json.Length + 1];
        json.CopyTo(line);
        line[^1] = (byte)'\n';
        return line;
    }
}
