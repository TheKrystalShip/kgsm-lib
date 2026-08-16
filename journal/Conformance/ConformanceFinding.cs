namespace TheKrystalShip.KGSM.Conformance;

/// <summary>
/// One way in which something written to a journal does not match the envelope contract.
/// </summary>
/// <remarks>
/// A finding locates itself precisely — producer, segment, line — because the point of the check is
/// to be actionable. "Some producer writes a bad timestamp" sends somebody reading five journals;
/// this sends them to a line.
/// </remarks>
/// <param name="Producer">The producer whose journal it was found in.</param>
/// <param name="Segment">The segment file name, or null for a finding about the journal itself.</param>
/// <param name="Line">The 1-based line number, or 0 for a finding about the journal itself.</param>
/// <param name="Rule">The <see cref="ConformanceRule"/> that was broken.</param>
/// <param name="Detail">What was found, in enough detail to act on without opening the file.</param>
public sealed record ConformanceFinding(
    string Producer,
    string? Segment,
    int Line,
    string Rule,
    string Detail)
{
    /// <summary>One line naming the rule and where it broke.</summary>
    /// <returns>The finding as a single readable line.</returns>
    public override string ToString()
    {
        string where = Segment is null ? Producer : $"{Producer}/{Segment}:{Line}";
        return $"[{Rule}] {where}: {Detail}";
    }
}
