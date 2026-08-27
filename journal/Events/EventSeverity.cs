namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// How much an event matters, as the producer that raised it judges.
/// </summary>
/// <remarks>
/// <para>
/// <b>The producer owns this and nothing downstream second-guesses it.</b> A retention sweep is
/// routine and the scheduler is the only thing that knows so; an uninstall is not and the engine is
/// the only thing that knows that. Reconstructing either from proxies further down produces a
/// judgement nobody made.
/// </para>
/// <para>
/// <b>An outcome is not a severity.</b> Whether something worked is <see cref="EventOutcome"/>, on
/// its own field: a backup created and a config key set are both routine, and they differ in how
/// they went rather than in how much they matter.
/// </para>
/// <para>
/// One enum, defined once, because a scale restated per producer is two vocabularies — and a reader
/// that meets both is holding a translation table again.
/// </para>
/// </remarks>
public enum EventSeverity
{
    /// <summary>A fact worth recording. The weight of most of what a producer writes.</summary>
    Info = 0,

    /// <summary>Something is off, reduced, refused, or somebody's authority changed.</summary>
    Warn = 1,

    /// <summary>Irreversible destruction, or a failure.</summary>
    Danger = 2,
}

/// <summary>The wire spelling of <see cref="EventSeverity"/>, which is the only spelling.</summary>
public static class EventSeverities
{
    /// <summary>Every value's wire spelling, in declaration order.</summary>
    public static readonly IReadOnlyList<string> All = ["info", "warn", "danger"];

    /// <summary>The wire spelling of one value.</summary>
    /// <param name="severity">The value.</param>
    /// <returns>Its lowercase wire spelling.</returns>
    /// <remarks>
    /// Written out rather than lowercasing <c>ToString()</c>: this assembly is AOT-compatible, and an
    /// explicit switch costs no enum metadata and cannot be changed by a rename.
    /// </remarks>
    public static string ToWire(this EventSeverity severity) => severity switch
    {
        EventSeverity.Warn => "warn",
        EventSeverity.Danger => "danger",
        _ => "info",
    };

    /// <summary>Reads a wire spelling.</summary>
    /// <param name="value">The spelling as a line carries it.</param>
    /// <param name="severity">The value, when it is one.</param>
    /// <returns>True when <paramref name="value"/> is a defined spelling.</returns>
    public static bool TryParse(string? value, out EventSeverity severity)
    {
        switch (value)
        {
            case "info": severity = EventSeverity.Info; return true;
            case "warn": severity = EventSeverity.Warn; return true;
            case "danger": severity = EventSeverity.Danger; return true;
            default: severity = EventSeverity.Info; return false;
        }
    }
}
