namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// How an event went, separately from how much it matters.
/// </summary>
/// <remarks>
/// <see cref="EventSeverity"/> answers the other question. The two are orthogonal on purpose: a
/// backup created and a config key set are both <see cref="EventSeverity.Info"/> and differ here,
/// while an uninstall that worked and one that failed are both about a machine being taken apart and
/// differ in severity rather than in kind.
/// </remarks>
public enum EventOutcome
{
    /// <summary>The event reports neither a success nor a failure — it reports a fact.</summary>
    Neutral = 0,

    /// <summary>Something completed, and completing was the good result.</summary>
    Success = 1,

    /// <summary>Something did not do what it set out to do.</summary>
    Failure = 2,
}

/// <summary>The wire spelling of <see cref="EventOutcome"/>, which is the only spelling.</summary>
public static class EventOutcomes
{
    /// <summary>Every value's wire spelling, in declaration order.</summary>
    public static readonly IReadOnlyList<string> All = ["neutral", "success", "failure"];

    /// <summary>The wire spelling of one value.</summary>
    /// <param name="outcome">The value.</param>
    /// <returns>Its lowercase wire spelling.</returns>
    public static string ToWire(this EventOutcome outcome) => outcome switch
    {
        EventOutcome.Success => "success",
        EventOutcome.Failure => "failure",
        _ => "neutral",
    };

    /// <summary>Reads a wire spelling.</summary>
    /// <param name="value">The spelling as a line carries it.</param>
    /// <param name="outcome">The value, when it is one.</param>
    /// <returns>True when <paramref name="value"/> is a defined spelling.</returns>
    public static bool TryParse(string? value, out EventOutcome outcome)
    {
        switch (value)
        {
            case "neutral": outcome = EventOutcome.Neutral; return true;
            case "success": outcome = EventOutcome.Success; return true;
            case "failure": outcome = EventOutcome.Failure; return true;
            default: outcome = EventOutcome.Neutral; return false;
        }
    }
}
