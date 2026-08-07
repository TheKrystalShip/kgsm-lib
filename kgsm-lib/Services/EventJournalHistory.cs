using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Answers history queries by reading the engine's journal segments directly.
/// </summary>
/// <remarks>
/// <para>
/// Three properties of the journal make a per-query scan cheap enough that an index would be
/// optimizing a problem that does not exist. Segments are named by date, so a bounded window
/// is narrowed to a handful of files by their <em>names</em>, before one is opened. Segments
/// are visited newest-first and the scan stops the moment the page is full, so a typical
/// "recent events" read touches one file. And each segment is streamed forward once, with
/// matches kept in a ring buffer the size of the page — which by construction holds that
/// segment's newest matches — so memory is bounded by the page size rather than the segment,
/// and no file is ever read backwards or loaded whole.
/// </para>
/// <para>
/// The scan is bounded by <see cref="KgsmOptions.EventHistoryScanBudgetBytes"/>. Hitting it
/// ends the read and sets <see cref="EventHistoryPage.Truncated"/>, so a bounded answer is
/// never handed back as an exhaustive one.
/// </para>
/// </remarks>
public sealed class EventJournalHistory : IEventJournalHistory
{
    private const string SegmentPattern = "*.ndjson";
    private const int ReadBufferSize = 64 * 1024;

    /// <summary>
    /// How far either side of a query's window the candidate segments reach.
    /// <para>
    /// The engine names a segment from the clock when an emit begins, so an emit that straddles
    /// midnight lands in the previous day's file. A segment can therefore hold an event dated
    /// just outside the day its name claims, and pruning candidates on the name alone would
    /// skip it. One day of slack covers that by orders of magnitude, and costs at most two extra
    /// files.
    /// </para>
    /// </summary>
    private static readonly TimeSpan SegmentSlack = TimeSpan.FromDays(1);

    private readonly string _directory;
    private readonly long _budgetBytes;
    private readonly ILogger<EventJournalHistory> _logger;

    /// <summary>
    /// Initializes a new reader over the journal directory named by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">KGSM options supplying the journal directory and the scan budget.</param>
    /// <param name="logger">The logger to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the options carry no journal directory.</exception>
    public EventJournalHistory(KgsmOptions options, ILogger<EventJournalHistory> logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        if (string.IsNullOrWhiteSpace(options.EventJournalDirectory))
            throw new ArgumentException("Event journal directory cannot be null, empty, or whitespace.", nameof(options));

        _directory = options.EventJournalDirectory;
        _budgetBytes = options.EventHistoryScanBudgetBytes;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EventHistoryPage> QueryAsync(EventHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query, nameof(query));

        IReadOnlyList<string> segments = ListSegments();
        if (segments.Count == 0)
        {
            // No directory, no segments, or a directory that could not be listed. A host that
            // has never emitted an event is indistinguishable from one whose journal cannot be
            // read, and neither can answer for history — so both report the same way.
            _logger.LogDebug("No event journal segments at {Directory}", _directory);
            return EventHistoryPage.Unreadable;
        }

        int limit = query.Limit <= 0
            ? EventHistoryQuery.DefaultLimit
            : Math.Min(query.Limit, EventHistoryQuery.MaxLimit);

        DateTimeOffset? coverageFrom = await ReadCoverageFromAsync(segments[0], cancellationToken).ConfigureAwait(false);

        // The cursor names a position; the ORDER is by timestamp. Resolving the cursor's own
        // timestamp lets the filter below use the same composite predicate the ordering does,
        // rather than assuming file order and timestamp order agree everywhere.
        Cursor? before = null;
        if (!string.IsNullOrEmpty(query.Before))
        {
            before = await ResolveCursorAsync(query.Before, cancellationToken).ConfigureAwait(false);
            if (before is null)
            {
                _logger.LogWarning(
                    "Event history cursor {Cursor} does not name a readable journal position — returning the newest page instead",
                    query.Before);
            }
        }

        var results = new List<EventHistoryEntry>(limit);
        long scanned = 0;
        bool truncated = false;

        foreach (string segment in CandidateSegments(segments, query, before))
        {
            if (results.Count >= limit)
                break;

            (List<EventHistoryEntry> matches, long bytes, bool budgetHit) = await ScanSegmentAsync(
                segment, query, before, limit - results.Count, _budgetBytes - scanned, cancellationToken)
                .ConfigureAwait(false);

            scanned += bytes;
            results.AddRange(matches);

            if (budgetHit)
            {
                truncated = true;
                _logger.LogWarning(
                    "Event history scan stopped at its {Budget}-byte budget while reading {Segment}; the page is a prefix of the answer",
                    _budgetBytes, segment);
                break;
            }
        }

        // Segments are visited newest-first and each contributes its own newest-first block, so
        // the concatenation is already in order under the normal case where file order and
        // timestamp order agree. A final sort makes that independent of the assumption, at the
        // cost of ordering one page.
        results.Sort(static (a, b) =>
        {
            int byTime = b.Ts.CompareTo(a.Ts);
            return byTime != 0 ? byTime : string.CompareOrdinal(b.Id, a.Id);
        });

        if (results.Count > limit)
            results.RemoveRange(limit, results.Count - limit);

        // A full page might have more behind it; a partial one is the end of the road, so its
        // cursor is honestly null rather than pointing at an empty result.
        string? next = results.Count == limit && results.Count > 0 ? results[^1].Id : null;

        return new EventHistoryPage(results, next, coverageFrom, truncated, true);
    }

    /// <summary>A resolved pagination cursor: the position named, and the timestamp found there.</summary>
    private readonly record struct Cursor(string Id, DateTimeOffset Ts, string Segment);

    /// <summary>
    /// Reads the event a cursor id points at, to recover the timestamp that orders it. Returns
    /// null when the id is not a position id or names a position the journal no longer holds —
    /// a cursor into a pruned segment, which is unusable rather than wrong.
    /// </summary>
    private async Task<Cursor?> ResolveCursorAsync(string id, CancellationToken token)
    {
        if (!AuditId.TryParsePosition(id, out string stem, out long offset))
            return null;

        string path = Path.Combine(_directory, stem + ".ndjson");
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, ReadBufferSize, useAsync: true);

            if (offset >= stream.Length)
                return null;

            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(line))
                return null;

            EventWrapper? wrapper = Deserialize(line, path, offset);
            return wrapper?.Timestamp is { } ts ? new Cursor(id, ts, stem) : null;
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not resolve the event history cursor {Cursor}", id);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Not permitted to resolve the event history cursor {Cursor}", id);
            return null;
        }
    }

    /// <summary>
    /// The segments that can contain events matching the query, newest first.
    /// </summary>
    /// <remarks>
    /// Names are dates, so the window bounds prune the list without opening anything. The bounds
    /// are widened by <see cref="SegmentSlack"/> and the cursor's own segment is always kept:
    /// this is an optimization over a filter that is applied per event regardless, so it is
    /// deliberately generous — including a segment that turns out to hold nothing costs one file
    /// read, while excluding one that did costs a missing event.
    /// </remarks>
    private static IEnumerable<string> CandidateSegments(
        IReadOnlyList<string> segments, EventHistoryQuery query, Cursor? before)
    {
        DateOnly? from = query.SinceMs is { } since
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(since).UtcDateTime - SegmentSlack)
            : null;

        DateOnly? to = query.UntilMs is { } until
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(until).UtcDateTime + SegmentSlack)
            : null;

        // Nothing at or after the cursor's own position can qualify, so segments newer than its
        // own are skipped — with the same one-day slack, since a straggler write can date an
        // event slightly outside the segment that holds it.
        DateOnly? cursorCeiling = before is { } c && DateOnly.TryParse(c.Segment, out DateOnly cursorDate)
            ? cursorDate.AddDays(1)
            : null;

        for (int i = segments.Count - 1; i >= 0; i--)
        {
            string segment = segments[i];

            // A segment whose name is not a date is outside the engine's own convention; it is
            // always scanned rather than guessed about.
            if (!DateOnly.TryParse(Path.GetFileNameWithoutExtension(segment), out DateOnly date))
            {
                yield return segment;
                continue;
            }

            if (from is { } lower && date < lower) continue;
            if (to is { } upper && date > upper) continue;
            if (cursorCeiling is { } ceiling && date > ceiling) continue;

            yield return segment;
        }
    }

    /// <summary>
    /// Streams one segment forward, keeping the newest <paramref name="wanted"/> matches.
    /// </summary>
    /// <returns>
    /// The matches newest-first, how many bytes were read, and whether the budget ran out
    /// mid-segment.
    /// </returns>
    private async Task<(List<EventHistoryEntry> Matches, long Bytes, bool BudgetHit)> ScanSegmentAsync(
        string segment, EventHistoryQuery query, Cursor? before, int wanted, long budget, CancellationToken token)
    {
        var matches = new List<EventHistoryEntry>();
        string path = Path.Combine(_directory, segment);
        string stem = Path.GetFileNameWithoutExtension(segment);

        if (budget <= 0)
            return (matches, 0, true);

        FileStream stream;
        try
        {
            // The engine appends to this file while it is open and retention may unlink it
            // underneath the read; neither is an error.
            stream = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, ReadBufferSize, useAsync: true);
        }
        catch (FileNotFoundException)
        {
            return (matches, 0, false);
        }
        catch (DirectoryNotFoundException)
        {
            return (matches, 0, false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Not permitted to read the event journal segment {Segment}", segment);
            return (matches, 0, false);
        }

        // The ring holds at most `wanted` entries: enqueueing past that drops the oldest, so
        // what survives a forward pass is exactly the segment's newest matches.
        var ring = new Queue<EventHistoryEntry>(wanted);
        long bytes = 0;
        bool budgetHit = false;

        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, ReadBufferSize);

            long offset = 0;
            string? line;

            while ((line = await reader.ReadLineAsync(token).ConfigureAwait(false)) is not null)
            {
                // The offset of the NEXT line, tracked as the encoded length plus the newline.
                // Every event is one whole line and the engine never rewrites a segment, so this
                // stays exact for the lifetime of the id it produces.
                long lineStart = offset;
                offset += Encoding.UTF8.GetByteCount(line) + 1;

                bytes = offset;
                if (bytes > budget)
                {
                    budgetHit = true;
                    break;
                }

                if (line.Length == 0)
                    continue;

                if (!CouldMatch(line, query))
                    continue;

                EventWrapper? wrapper = Deserialize(line, path, lineStart);
                if (wrapper is null)
                    continue;

                if (wrapper.Timestamp is not { } ts)
                {
                    // An event with no timestamp cannot be placed in a time-ordered history, and
                    // inventing one would put a fabricated moment in the audit trail. It is
                    // reported and skipped.
                    _logger.LogWarning(
                        "Event journal {Segment}+{Offset} carries no Timestamp and is absent from history",
                        segment, lineStart);
                    continue;
                }

                string id = AuditId.ForPosition(stem, lineStart);
                if (!Matches(wrapper, ts, id, query, before))
                    continue;

                ring.Enqueue(new EventHistoryEntry(
                    id, ts, wrapper.EventType,
                    ReadName(wrapper.Data, "InstanceName"),
                    ReadName(wrapper.Data, "BlueprintName"),
                    wrapper.Actor, wrapper.Origin, wrapper.Hostname,
                    wrapper.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? null
                        : wrapper.Data));

                if (ring.Count > wanted)
                    ring.Dequeue();
            }
        }

        matches.AddRange(ring);
        matches.Reverse();
        return (matches, bytes, budgetHit);
    }

    /// <summary>
    /// A cheap reject for a line that cannot possibly match, before the cost of parsing it.
    /// </summary>
    /// <remarks>
    /// Substring containment only, never a shape assumption: a filter's value appearing nowhere
    /// in the line means no parse of it can match, whatever the formatting. The reverse is not
    /// claimed — a line that passes here is still checked properly after parsing — so this can
    /// only ever save work, never change an answer.
    /// </remarks>
    private static bool CouldMatch(string line, EventHistoryQuery query)
    {
        if (!string.IsNullOrEmpty(query.Type) && !line.Contains(query.Type, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(query.Instance) && !line.Contains(query.Instance, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(query.Blueprint) && !line.Contains(query.Blueprint, StringComparison.Ordinal))
            return false;

        return true;
    }

    /// <summary>The authoritative filter, applied to a parsed envelope.</summary>
    private static bool Matches(
        EventWrapper wrapper, DateTimeOffset ts, string id, EventHistoryQuery query, Cursor? before)
    {
        if (!string.IsNullOrEmpty(query.Type)
            && !string.Equals(wrapper.EventType, query.Type, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(query.Instance)
            && !string.Equals(ReadName(wrapper.Data, "InstanceName"), query.Instance, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(query.Blueprint)
            && !string.Equals(ReadName(wrapper.Data, "BlueprintName"), query.Blueprint, StringComparison.Ordinal))
            return false;

        long tsMs = ts.ToUnixTimeMilliseconds();
        if (query.SinceMs is { } since && tsMs < since) return false;
        if (query.UntilMs is { } until && tsMs > until) return false;

        // The same composite predicate the ordering uses: strictly older, or the same instant
        // and a lower id. Matching the sort exactly is what stops paging from repeating or
        // skipping an event where several share a timestamp.
        if (before is { } cursor)
        {
            long cursorMs = cursor.Ts.ToUnixTimeMilliseconds();
            if (tsMs > cursorMs) return false;
            if (tsMs == cursorMs && string.CompareOrdinal(id, cursor.Id) >= 0) return false;
        }

        return true;
    }

    /// <summary>
    /// The timestamp of the oldest event the journal still holds — what the history can answer
    /// for. Read from the first line of the oldest segment, because retention deletes whole
    /// segments oldest-first and never truncates one.
    /// </summary>
    private async Task<DateTimeOffset?> ReadCoverageFromAsync(string oldest, CancellationToken token)
    {
        string path = Path.Combine(_directory, oldest);

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, ReadBufferSize, useAsync: true);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(token).ConfigureAwait(false)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                return Deserialize(line, path, 0)?.Timestamp;
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not read the oldest event journal segment {Segment}", oldest);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Not permitted to read the oldest event journal segment {Segment}", oldest);
        }

        return null;
    }

    /// <summary>
    /// Deserializes one journal line, or null when it cannot be read. A malformed line is
    /// reported and skipped — one bad line never fails a whole query.
    /// </summary>
    private EventWrapper? Deserialize(string line, string path, long offset)
    {
        try
        {
            EventWrapper? wrapper = JsonSerializer.Deserialize(line, KgsmJsonContext.Default.EventWrapper);
            if (wrapper is null || string.IsNullOrEmpty(wrapper.EventType))
            {
                _logger.LogWarning("Event journal {Path}+{Offset} holds no readable event", path, offset);
                return null;
            }

            return wrapper;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Event journal {Path}+{Offset} is not valid JSON", path, offset);
            return null;
        }
    }

    /// <summary>Reads a string property from an event payload, or null when it carries none.</summary>
    private static string? ReadName(JsonElement data, string property)
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            string? name = value.GetString();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        return null;
    }

    /// <summary>
    /// The journal's segment file names, oldest first. Names are dates, so ordinal sort order is
    /// chronological order.
    /// </summary>
    private IReadOnlyList<string> ListSegments()
    {
        if (!Directory.Exists(_directory))
            return [];

        try
        {
            string[] paths = Directory.GetFiles(_directory, SegmentPattern);
            string[] names = new string[paths.Length];

            for (int i = 0; i < paths.Length; i++)
                names[i] = Path.GetFileName(paths[i]);

            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to list event journal segments in {Directory}", _directory);
            return [];
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Not permitted to read the event journal directory {Directory}", _directory);
            return [];
        }
    }
}
