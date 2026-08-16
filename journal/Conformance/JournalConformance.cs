using System.Globalization;
using System.Text.Json;
using TheKrystalShip.KGSM.Events;
using TheKrystalShip.KGSM.Services;

namespace TheKrystalShip.KGSM.Conformance;

/// <summary>
/// Reads what producers actually wrote and reports where it does not match the envelope contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>Producer-agnostic by construction.</b> Nothing here knows which components exist: it is handed
/// journals and checks whatever it is handed. A producer added to this ecosystem later is covered the
/// moment its journal exists, without anybody remembering to cover it — which is the property that
/// matters, because the drift this catches is precisely the kind nobody thinks to look for.
/// </para>
/// <para>
/// <b>Mechanism, never policy.</b> No rule looks at an event type or a payload field. What a producer
/// records, and when, is its own business; that it spells the envelope the way every reader parses it
/// is not.
/// </para>
/// <para>
/// <b>An old line is allowed to look old.</b> A line records what the build that wrote it produced,
/// so a journal's history legitimately contains shapes a current build would no longer write. A caller
/// checking a host for conformance therefore samples the newest lines — the ones the deployed builds
/// wrote — and that is why the sample size is the caller's to choose rather than "everything".
/// </para>
/// </remarks>
public static class JournalConformance
{
    /// <summary>Envelope fields every line carries.</summary>
    public static readonly IReadOnlyList<string> RequiredFields = ["V", "EventType", "Data", "Timestamp"];

    /// <summary>
    /// Envelope fields a line may carry, including the ones reserved for correlation.
    /// </summary>
    /// <remarks>
    /// The reserved three are listed so a producer that starts populating them is not reported as
    /// having invented a field — they are part of the contract whether or not anything writes them.
    /// </remarks>
    public static readonly IReadOnlyList<string> OptionalFields =
        ["Actor", "Origin", "Hostname", "ProducerVersion", "OpId", "RunId", "During"];

    /// <summary>How many lines of a journal a host check reads when the caller names no number.</summary>
    /// <remarks>
    /// One is enough to catch a producer's spelling, because a producer spells every line the same
    /// way. More is for a caller that wants the shape of a whole day.
    /// </remarks>
    public const int DefaultLinesPerJournal = 1;

    private static readonly HashSet<string> KnownFields =
        new(RequiredFields.Concat(OptionalFields), StringComparer.Ordinal);

    /// <summary>
    /// Checks one journal line.
    /// </summary>
    /// <param name="producer">The producer whose journal the line came from.</param>
    /// <param name="segment">The segment file name, for locating a finding.</param>
    /// <param name="lineNumber">The 1-based line number, for locating a finding.</param>
    /// <param name="line">The raw line.</param>
    /// <returns>Every rule the line breaks; empty when it conforms.</returns>
    public static IReadOnlyList<ConformanceFinding> CheckLine(
        string producer, string? segment, int lineNumber, string? line)
    {
        var findings = new List<ConformanceFinding>();
        void Add(string rule, string detail) =>
            findings.Add(new ConformanceFinding(producer, segment, lineNumber, rule, detail));

        if (string.IsNullOrWhiteSpace(line))
        {
            Add(ConformanceRule.LineParses, "the line is blank");
            return findings;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            // A malformed line is not cosmetic: a consumer's cursor is a byte offset, so one bad line
            // desynchronizes every reader past it. Nothing else about the line can be checked.
            Add(ConformanceRule.LineParses, $"not valid JSON ({ex.Message})");
            return findings;
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                Add(ConformanceRule.LineParses, $"expected a JSON object, found {root.ValueKind}");
                return findings;
            }

            CheckSchemaVersion(root, Add);
            CheckEventType(root, Add);
            CheckData(root, Add);
            CheckTimestamp(root, Add);
            CheckAbsentSpelling(root, Add);
            CheckActor(root, Add);
            CheckProducerVersion(root, Add);
            CheckUnknownFields(root, Add);
        }

        return findings;
    }

    /// <summary>
    /// Checks one producer's journal: its layout, its segment names, and its most recent lines.
    /// </summary>
    /// <param name="producer">The producer id, as the caller established it.</param>
    /// <param name="directory">The directory that producer appends to.</param>
    /// <param name="lines">
    /// How many of the newest lines to read. Zero or less reads none, which checks layout only.
    /// </param>
    /// <returns>What was found, and what the journal said about the host it was written on.</returns>
    /// <exception cref="ArgumentException">Thrown when the producer id or directory is blank.</exception>
    public static JournalReport CheckJournal(
        string producer, string directory, int lines = DefaultLinesPerJournal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(producer, nameof(producer));
        ArgumentException.ThrowIfNullOrWhiteSpace(directory, nameof(directory));

        var findings = new List<ConformanceFinding>();

        CheckDirectoryAttribution(producer, directory, findings);

        string[] segments;

        try
        {
            if (!Directory.Exists(directory))
            {
                // A producer with no directory has recorded nothing, which is an honest state and not
                // this check's business. A caller that expected it to exist says so itself.
                return new JournalReport(producer, directory, findings, null, 0, null);
            }

            segments = Directory.GetFiles(directory, "*.ndjson");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            findings.Add(new ConformanceFinding(
                producer, null, 0, ConformanceRule.JournalReadable,
                $"the journal at {directory} could not be listed ({ex.Message})"));

            return new JournalReport(producer, directory, findings, null, 0, null);
        }

        Array.Sort(segments, StringComparer.Ordinal);
        CheckSegmentNames(producer, segments, findings);

        if (segments.Length == 0 || lines <= 0)
            return new JournalReport(producer, directory, findings, null, 0, null);

        string newest = segments[^1];
        string name = Path.GetFileName(newest);

        (int Number, string Text)[] tail;

        try
        {
            tail = Tail(newest, lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            findings.Add(new ConformanceFinding(
                producer, name, 0, ConformanceRule.JournalReadable,
                $"the segment could not be read ({ex.Message})"));

            return new JournalReport(producer, directory, findings, null, 0, name);
        }

        string? hostname = null;

        foreach ((int number, string text) in tail)
        {
            findings.AddRange(CheckLine(producer, name, number, text));
            hostname ??= HostnameOf(text);
        }

        return new JournalReport(producer, directory, findings, hostname, tail.Length, name);
    }

    /// <summary>
    /// Checks every journal on a host, including the things only comparing them can reveal.
    /// </summary>
    /// <remarks>
    /// The cross-producer half is the reason this is not just <see cref="CheckJournal"/> in a loop:
    /// a hostname that two producers spell differently is conformant in each journal read alone and
    /// wrong the moment they are merged, which is how every consumer reads them.
    /// </remarks>
    /// <param name="journals">The journals to check, as producer and directory.</param>
    /// <param name="lines">How many of the newest lines to read from each.</param>
    /// <returns>Every journal's report, plus the findings that only exist across journals.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="journals"/> is null.</exception>
    public static HostReport CheckHost(
        IEnumerable<JournalTarget> journals, int lines = DefaultLinesPerJournal)
    {
        ArgumentNullException.ThrowIfNull(journals, nameof(journals));

        List<JournalReport> reports = [.. journals.Select(j => CheckJournal(j.Producer, j.Directory, lines))];

        var crossJournal = new List<ConformanceFinding>();

        // The hostname every producer that named one agreed on. Producers that named none are not
        // disagreeing — the field is optional, and absence is not a competing answer.
        List<JournalReport> named = [.. reports.Where(static r => r.Hostname is not null)];
        string[] distinct = [.. named.Select(static r => r.Hostname!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        if (distinct.Length > 1)
        {
            string spread = string.Join(", ", named.Select(static r => $"{r.Producer}={r.Hostname}"));

            foreach (JournalReport report in named)
            {
                crossJournal.Add(new ConformanceFinding(
                    report.Producer, report.Segment, 0, ConformanceRule.HostnameAgrees,
                    $"producers on one host name it {distinct.Length} different ways ({spread})"));
            }
        }

        return new HostReport(reports, crossJournal);
    }

    private static void CheckSchemaVersion(JsonElement root, Action<string, string> add)
    {
        if (!root.TryGetProperty("V", out JsonElement version))
        {
            add(ConformanceRule.SchemaVersion, "no V — a reader cannot tell which contract this is");
            return;
        }

        if (version.ValueKind != JsonValueKind.Number)
        {
            add(ConformanceRule.SchemaVersion, $"V is {version.ValueKind}, expected a number");
            return;
        }

        if (!version.TryGetInt32(out int value) || value != EventJournalWriter.SchemaVersion)
        {
            add(ConformanceRule.SchemaVersion,
                $"V is {version} — this reader understands {EventJournalWriter.SchemaVersion}");
        }
    }

    private static void CheckEventType(JsonElement root, Action<string, string> add)
    {
        if (!root.TryGetProperty("EventType", out JsonElement type))
        {
            add(ConformanceRule.EventType, "no EventType");
            return;
        }

        if (type.ValueKind != JsonValueKind.String)
        {
            add(ConformanceRule.EventType, $"EventType is {type.ValueKind}, expected a string");
            return;
        }

        string value = type.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            add(ConformanceRule.EventType, "EventType is blank");
            return;
        }

        // Dash on a command line, underscore on the wire. A type that reaches a journal still dashed
        // is a type no consumer matches, and it fails silently — a subscriber simply never fires.
        if (value.Contains('-', StringComparison.Ordinal))
            add(ConformanceRule.EventType, $"'{value}' is dash-separated; the wire spells types with underscores");

        if (value.Any(char.IsUpper))
            add(ConformanceRule.EventType, $"'{value}' is not lowercase");

        if (value.Any(char.IsWhiteSpace))
            add(ConformanceRule.EventType, $"'{value}' contains whitespace");
    }

    private static void CheckData(JsonElement root, Action<string, string> add)
    {
        if (!root.TryGetProperty("Data", out JsonElement data))
        {
            add(ConformanceRule.Data, "no Data — every reader keys an event's subject off a property of it");
            return;
        }

        if (data.ValueKind != JsonValueKind.Object)
            add(ConformanceRule.Data, $"Data is {data.ValueKind}, expected an object");
    }

    private static void CheckTimestamp(JsonElement root, Action<string, string> add)
    {
        if (!root.TryGetProperty("Timestamp", out JsonElement stamp))
        {
            add(ConformanceRule.Timestamp, "no Timestamp");
            return;
        }

        if (stamp.ValueKind != JsonValueKind.String)
        {
            add(ConformanceRule.Timestamp, $"Timestamp is {stamp.ValueKind}, expected a string");
            return;
        }

        string value = stamp.GetString() ?? string.Empty;

        // Exact, not "parseable". Several journals merged on second granularity order arbitrarily
        // inside each second, which is exactly where causally adjacent events sit — so a producer
        // writing a coarser or differently-shaped stamp is not a cosmetic difference.
        if (!DateTimeOffset.TryParseExact(
                value, EventJournalWriter.TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
        {
            add(ConformanceRule.Timestamp,
                $"'{value}' is not millisecond-precision UTC ({EventJournalWriter.TimestampFormat})");
        }
    }

    private static void CheckAbsentSpelling(JsonElement root, Action<string, string> add)
    {
        foreach (string field in OptionalFields)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
                continue;

            if (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
            {
                add(ConformanceRule.AbsentSpelling,
                    $"{field} is an empty string — absence is spelled by omitting the field or writing null");
            }
        }
    }

    private static void CheckActor(JsonElement root, Action<string, string> add)
    {
        if (!root.TryGetProperty("Actor", out JsonElement actor) || actor.ValueKind != JsonValueKind.String)
            return;

        string value = actor.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return;

        // A bare name is a local OS user and is meant: the engine writes one. What cannot be read is a
        // half-written qualified actor, where one side of the separator names nobody.
        int separator = value.IndexOf(':', StringComparison.Ordinal);

        if (separator < 0)
            return;

        if (separator == 0 || separator == value.Length - 1)
            add(ConformanceRule.Actor, $"'{value}' is qualified but one side of the ':' is empty");
    }

    private static void CheckProducerVersion(JsonElement root, Action<string, string> add)
    {
        if (!root.TryGetProperty("ProducerVersion", out JsonElement version)
            || version.ValueKind != JsonValueKind.String)
        {
            return;
        }

        string value = version.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return;

        if (IsFourPartAssemblyVersion(value))
        {
            add(ConformanceRule.ProducerVersionShape,
                $"'{value}' is a four-part assembly version — no released package is numbered that way, "
                + "so it matches no tag, no pin and no other producer's stamp");
        }
    }

    /// <summary>
    /// Whether a version is the four-part number an assembly carries rather than the one it ships as.
    /// </summary>
    /// <remarks>
    /// The single measurable shape of the drift: an assembly exposes both, and a producer reaching for
    /// the wrong one stamps <c>2.3.0.0</c> where every other producer stamps <c>2.3.0+&lt;sha&gt;</c>.
    /// Nothing else is rejected — a version is otherwise whatever a repo numbers its release.
    /// </remarks>
    /// <param name="version">The version as written.</param>
    /// <returns>True when it is four all-numeric dot-separated parts.</returns>
    public static bool IsFourPartAssemblyVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        string[] parts = version.Split('.');

        if (parts.Length != 4)
            return false;

        foreach (string part in parts)
        {
            if (part.Length == 0 || !part.All(char.IsAsciiDigit))
                return false;
        }

        return true;
    }

    private static void CheckUnknownFields(JsonElement root, Action<string, string> add)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!KnownFields.Contains(property.Name))
            {
                add(ConformanceRule.UnknownField,
                    $"'{property.Name}' is not an envelope field; event-specific values belong in Data");
            }
        }
    }

    private static void CheckDirectoryAttribution(
        string producer, string directory, List<ConformanceFinding> findings)
    {
        string? attributed = JournalLayout.ProducerOf(directory);

        // Null is not a fault. A journal at a configured path outside the state root is invisible to a
        // scan and is instead named explicitly by the consumer that knows of it, which is how the
        // engine's configurable directory works. A non-null answer that disagrees is the fault: it
        // means a scan of this host would file these lines under somebody else's name.
        if (attributed is not null && !string.Equals(attributed, producer, StringComparison.Ordinal))
        {
            findings.Add(new ConformanceFinding(
                producer, null, 0, ConformanceRule.ProducerMatchesDirectory,
                $"lines here would be attributed to '{attributed}', because that is what {directory} says"));
        }
    }

    private static void CheckSegmentNames(
        string producer, string[] segments, List<ConformanceFinding> findings)
    {
        foreach (string segment in segments)
        {
            string stem = Path.GetFileNameWithoutExtension(segment);

            // A segment's age comes from its name, so a name that is not a date is a segment retention
            // cannot reason about — it is kept forever rather than deleted, which is the safe half of
            // the problem, but it also sorts unpredictably against the ones that are dates.
            if (!DateOnly.TryParseExact(stem, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                findings.Add(new ConformanceFinding(
                    producer, Path.GetFileName(segment), 0, ConformanceRule.SegmentName,
                    "a segment is named for the day it holds (yyyy-MM-dd.ndjson); retention ages it by that name"));
            }
        }
    }

    private static string? HostnameOf(string line)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return document.RootElement.TryGetProperty("Hostname", out JsonElement host)
                && host.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(host.GetString())
                    ? host.GetString()
                    : null;
        }
        catch (JsonException)
        {
            // Already reported by the line check; a malformed line simply says nothing about the host.
            return null;
        }
    }

    /// <summary>
    /// The last <paramref name="count"/> non-empty lines of a file, each with its 1-based position.
    /// </summary>
    /// <remarks>
    /// The number is the line's position in the file, not in the sample: a finding is only actionable
    /// if the number in it is the one an editor jumps to. Streamed rather than read whole, because a
    /// busy producer's segment is the one most worth checking and the least worth loading.
    /// </remarks>
    private static (int Number, string Text)[] Tail(string path, int count)
    {
        var window = new Queue<(int, string)>(count);
        int number = 0;

        foreach (string line in File.ReadLines(path))
        {
            number++;

            if (line.Length == 0)
                continue;

            if (window.Count == count)
                window.Dequeue();

            window.Enqueue((number, line));
        }

        return [.. window];
    }
}
