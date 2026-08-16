using System.Globalization;
using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Deletes journal segments past a producer's retention window.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whole segments, unlinked, never truncated.</b> Every consumer's position in a journal is a byte
/// offset into a named segment, so rewriting one in place — a log rotator's <c>copytruncate</c>, or
/// dropping the first N lines — invalidates every cursor into it and silently misplaces every event
/// after the cut. Removing the file whole is the one safe operation: a consumer whose cursor named it
/// reports a gap, which is a discontinuity it can say out loud rather than a stream that quietly
/// stopped meaning what it says.
/// </para>
/// <para>
/// <b>Age comes from the segment's name, not its mtime.</b> A segment named <c>2026-05-01</c> holds
/// that day's events whatever a filesystem thinks; an mtime is when the file was last written to,
/// which a restore, a copy or a backup tool changes without any event moving. The two agree on a
/// normally-operating host, and only the name still agrees on one that has been recovered.
/// </para>
/// <para>
/// <b>Only this producer's own segments are considered.</b> A file whose name is not a date is left
/// alone rather than guessed at — the directory belongs to one producer, but that is a reason to be
/// careful with it rather than a licence to delete whatever is in it.
/// </para>
/// </remarks>
public static class JournalRetention
{
    /// <summary>The window every producer keeps unless told otherwise.</summary>
    /// <remarks>
    /// Matches the engine's own <c>event_journal_retention_days</c>, so a merged page's coverage is
    /// bounded by one number rather than by whichever producer happened to be least generous.
    /// </remarks>
    public const int DefaultRetentionDays = 90;

    /// <summary>The segment-name format, which is also the segment's date.</summary>
    private const string SegmentDateFormat = "yyyy-MM-dd";

    /// <summary>The extension every segment carries.</summary>
    private const string SegmentExtension = ".ndjson";

    /// <summary>
    /// Removes segments in <paramref name="directory"/> older than <paramref name="retentionDays"/>.
    /// </summary>
    /// <remarks>
    /// Best-effort and never throws: retention is housekeeping, and a producer that cannot prune must
    /// still be able to record what it did. A segment that cannot be removed is logged and skipped.
    /// </remarks>
    /// <param name="directory">The producer's own journal directory.</param>
    /// <param name="retentionDays">
    /// Days to keep. <b>Zero or negative keeps everything</b> — the explicit opt-out for a host that
    /// retains its audit trail elsewhere.
    /// </param>
    /// <param name="now">The moment to measure age from.</param>
    /// <param name="logger">The logger to report removals and failures to.</param>
    /// <returns>How many segments were removed.</returns>
    public static int Prune(string directory, int retentionDays, DateTimeOffset now, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        if (retentionDays <= 0 || string.IsNullOrWhiteSpace(directory))
            return 0;

        // The oldest date still kept. A segment dated exactly on the boundary is kept: the window is
        // "this many days of history", and rounding it inward would quietly return one day less than
        // the number an operator configured.
        DateOnly cutoff = DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-retentionDays));

        string[] segments;

        try
        {
            if (!Directory.Exists(directory))
                return 0;

            segments = Directory.GetFiles(directory, "*" + SegmentExtension);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list the journal at {Path} to prune it", directory);
            return 0;
        }

        int removed = 0;

        foreach (string segment in segments)
        {
            if (DateOf(segment) is not { } dated || dated >= cutoff)
                continue;

            try
            {
                File.Delete(segment);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not prune the journal segment {Path}", segment);
            }
        }

        if (removed > 0)
        {
            logger.LogInformation(
                "Pruned {Count} journal segment(s) from {Path}, keeping {Days} days",
                removed, directory, retentionDays);
        }

        return removed;
    }

    /// <summary>
    /// The date a segment's name declares, or null when the name is not one this writer produced.
    /// </summary>
    private static DateOnly? DateOf(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);

        return DateOnly.TryParseExact(
            stem, SegmentDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }
}
