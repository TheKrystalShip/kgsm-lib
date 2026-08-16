using System.Text.Json;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Lifecycle;

/// <summary>
/// What a producer's own journal says it last reported about itself.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ <b>A process reports transitions from what it remembers, and a process that exits remembers
/// nothing.</b> Measured on the speech leaf: it reported a model it could not load, exited when idle,
/// woke with the model fixed, and wrote no recovery — because the fresh process had never seen the
/// fault. A journal that reports a fault and can never clear it is worse than one that reports
/// neither.
/// </para>
/// <para>
/// The journal is already the record, so the memory a process lacks is a tail-read away. Seeding
/// <see cref="LeafLifecycle"/> from this makes the transition model hold across a restart: a leaf that
/// wakes healthy after reporting a fault clears it, and one that wakes still broken says nothing,
/// which is what a leaf that exits between observations needs in both directions.
/// </para>
/// <para>
/// Only the newest segment is read, and only its lifecycle lines. A fault that predates the segment
/// boundary is not carried over — the alternative is opening older files on every start of a leaf that
/// may start dozens of times a day, to recover a fault nothing has re-observed since midnight.
/// </para>
/// </remarks>
public static class LeafState
{
    /// <summary>
    /// The components this producer last said were broken and has not since said were fixed.
    /// </summary>
    /// <param name="journalDirectory">The producer's own journal directory.</param>
    /// <returns>
    /// The degraded component ids. Empty when the journal is absent, unreadable, or says nothing is
    /// broken — a state that cannot be read is not evidence of a fault.
    /// </returns>
    public static IReadOnlyCollection<string> DegradedComponents(string? journalDirectory)
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
            return [];

        string[] segments;

        try
        {
            if (!Directory.Exists(journalDirectory))
                return [];

            segments = Directory.GetFiles(journalDirectory, "*.ndjson");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        if (segments.Length == 0)
            return [];

        Array.Sort(segments, StringComparer.Ordinal);

        var degraded = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (string line in File.ReadLines(segments[^1]))
                Apply(line, degraded);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Half a replay is worse than none: it would carry faults forward past the recovery that
            // cleared them. An unreadable journal seeds nothing.
            return [];
        }

        return degraded;
    }

    /// <summary>
    /// The state a producer's own journal directory implies, from its producer id.
    /// </summary>
    /// <param name="producer">The producer id.</param>
    /// <param name="stateRoot">Where state directories live. Null uses the default.</param>
    /// <returns>The degraded component ids.</returns>
    /// <exception cref="ArgumentException">Thrown when the producer id is unusable.</exception>
    public static IReadOnlyCollection<string> DegradedComponentsFor(string producer, string? stateRoot = null) =>
        DegradedComponents(JournalLayout.DirectoryFor(producer, stateRoot));

    /// <summary>Replays one line onto the running set, ignoring anything that is not a transition.</summary>
    private static void Apply(string line, HashSet<string> degraded)
    {
        if (line.Length == 0)
            return;

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("EventType", out JsonElement typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                return;
            }

            string? type = typeElement.GetString();

            // A leaf coming up is a fresh process with fresh observations, and everything before it
            // described a run that has ended. Resident leaves get their clean slate from this; a leaf
            // that exits when idle writes no ready line and carries its faults across, which is
            // exactly the difference between the two.
            if (type == LeafLifecycleEvents.Ready)
            {
                degraded.Clear();
                return;
            }

            bool broke = type == LeafLifecycleEvents.Degraded;
            bool fixedIt = type == LeafLifecycleEvents.Recovered;

            if (!broke && !fixedIt)
                return;

            if (!root.TryGetProperty("Data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty(LeafLifecycleFields.Component, out JsonElement component)
                || component.ValueKind != JsonValueKind.String
                || component.GetString() is not { Length: > 0 } name)
            {
                return;
            }

            if (broke)
                degraded.Add(name);
            else
                degraded.Remove(name);
        }
        catch (JsonException)
        {
            // A malformed line says nothing about this producer's state.
        }
    }
}
