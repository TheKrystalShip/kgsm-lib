namespace TheKrystalShip.KGSM.Conformance;

/// <summary>A journal to check: who writes it, and where.</summary>
/// <remarks>
/// Deliberately a plain pair rather than a type borrowed from the reader half. The checker is handed
/// journals and knows nothing else about the host, so it needs no dependency on whatever discovered
/// them — which is what lets a producer check its own journal without pulling in the reader.
/// </remarks>
/// <param name="Producer">The producer id.</param>
/// <param name="Directory">The directory it appends segments to.</param>
public readonly record struct JournalTarget(string Producer, string Directory);

/// <summary>
/// What checking one producer's journal found, and what it observed while looking.
/// </summary>
/// <param name="Producer">The producer whose journal was checked.</param>
/// <param name="Directory">The directory that was read.</param>
/// <param name="Findings">Every rule broken; empty when the journal conforms.</param>
/// <param name="Hostname">
/// The host the sampled lines name, or null when they named none. Reported rather than checked here,
/// because whether it is the right answer is only knowable against the other journals.
/// </param>
/// <param name="LinesChecked">
/// How many lines were actually read. Zero is not a pass — it means the producer has recorded nothing,
/// and a caller that expected otherwise has learned something the findings do not say.
/// </param>
/// <param name="Segment">The segment the lines came from, or null when there were none.</param>
public sealed record JournalReport(
    string Producer,
    string Directory,
    IReadOnlyList<ConformanceFinding> Findings,
    string? Hostname,
    int LinesChecked,
    string? Segment)
{
    /// <summary>Whether this journal broke no rule.</summary>
    public bool Conforms => Findings.Count == 0;
}

/// <summary>
/// What checking every journal on a host found.
/// </summary>
/// <param name="Journals">One report per journal, in the order they were given.</param>
/// <param name="CrossJournal">
/// Findings that do not belong to any single journal — the ones only visible by comparing producers,
/// which is how every consumer reads them.
/// </param>
public sealed record HostReport(
    IReadOnlyList<JournalReport> Journals,
    IReadOnlyList<ConformanceFinding> CrossJournal)
{
    /// <summary>Every finding, per-journal and cross-journal alike.</summary>
    public IReadOnlyList<ConformanceFinding> Findings =>
        [.. Journals.SelectMany(static j => j.Findings), .. CrossJournal];

    /// <summary>Whether the host broke no rule.</summary>
    public bool Conforms => Findings.Count == 0;

    /// <summary>How many lines were read in total.</summary>
    public int LinesChecked => Journals.Sum(static j => j.LinesChecked);

    /// <summary>
    /// The findings as lines, with a summary of what was actually read.
    /// </summary>
    /// <remarks>
    /// The coverage line is not decoration. A clean report over nothing looks exactly like a clean
    /// report over a host, and telling them apart is the whole reason this check exists.
    /// </remarks>
    /// <returns>A readable report.</returns>
    public string Describe()
    {
        string coverage = string.Join(", ", Journals.Select(static j =>
            $"{j.Producer}({j.LinesChecked})"));

        if (Conforms)
            return $"{Journals.Count} journal(s) conform; read {LinesChecked} line(s): {coverage}";

        return $"{Findings.Count} finding(s) across {Journals.Count} journal(s); read "
            + $"{LinesChecked} line(s): {coverage}"
            + Environment.NewLine
            + string.Join(Environment.NewLine, Findings.Select(static f => "  " + f));
    }
}
