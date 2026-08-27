using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Lifecycle;

/// <summary>
/// The four things a leaf says about its own state.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately four. Each event type costs a payload record, a catalog entry and a source-generated
/// JSON registration in the reader, so the test for adding one is whether a consumer would <em>act</em>
/// differently — not whether the transition exists.
/// </para>
/// <para>
/// ⚠ <b>Not <c>service_*</c>.</b> Those four events already exist and mean the opposite direction:
/// kgsm-api emits them to record what was done <em>to</em> a leaf, on somebody's instruction, with a
/// person as the actor. These are what a leaf did by itself. Both land in one merged page, so the
/// prefix is what keeps a reader from confusing "an admin restarted the monitor" with "the monitor
/// came back".
/// </para>
/// </remarks>
public static class LeafLifecycleEvents
{
    /// <summary>The leaf is up and able to do its job.</summary>
    public const string Ready = "leaf.ready";

    /// <summary>The leaf is up, and one part of its job is not working.</summary>
    public const string Degraded = "leaf.degraded";

    /// <summary>A part that was not working is working again.</summary>
    public const string Recovered = "leaf.recovered";

    /// <summary>
    /// The leaf is going away deliberately.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>There is deliberately no <c>leaf.stopped</c>.</b> The last thing a process can write is
    /// that it is stopping; whether it then stopped is not something it is around to say. A consumer
    /// that needs to know reads the next <see cref="Ready"/> — one with no <see cref="Stopping"/>
    /// before it is an unclean exit, and the journal is already the record that says so.
    /// </remarks>
    public const string Stopping = "leaf.stopping";

    /// <summary>
    /// The same four names, typed so the writer can take them.
    /// </summary>
    /// <remarks>
    /// Derived from the constants above rather than restated, so the name a leaf writes and the name a
    /// reader compares against cannot become two different strings. Readers match the constants; the
    /// emitter holds these.
    /// </remarks>
    public static class Names
    {
        /// <inheritdoc cref="LeafLifecycleEvents.Ready"/>
        public static readonly EventName Ready = EventName.Parse(LeafLifecycleEvents.Ready);

        /// <inheritdoc cref="LeafLifecycleEvents.Degraded"/>
        public static readonly EventName Degraded = EventName.Parse(LeafLifecycleEvents.Degraded);

        /// <inheritdoc cref="LeafLifecycleEvents.Recovered"/>
        public static readonly EventName Recovered = EventName.Parse(LeafLifecycleEvents.Recovered);

        /// <inheritdoc cref="LeafLifecycleEvents.Stopping"/>
        public static readonly EventName Stopping = EventName.Parse(LeafLifecycleEvents.Stopping);
    }
}

/// <summary>
/// The payload field names, held once so the emitter and the reader cannot spell them differently.
/// </summary>
/// <remarks>
/// ⚠ This exists because the equivalent drift is live elsewhere. A producer writing literal property
/// names in its own repo, and a payload class declaring matching properties in the reader's, are two
/// spellings of one set of names bound by nothing but case-insensitive matching — and a rename on
/// either side yields a field that silently reads back as its default. These four events are emitted
/// from seven repositories, so the names live here and the reader binds to them by
/// <c>JsonPropertyName</c>, which makes a rename move both sides at once.
/// </remarks>
public static class LeafLifecycleFields
{
    /// <summary>Milliseconds from process start to ready. Absent when the start could not be read.</summary>
    public const string StartupMs = "StartupMs";

    /// <summary>Why, in a sentence somebody can act on.</summary>
    public const string Detail = "Detail";

    /// <summary>Which part of the leaf's job. The dedup key, and what makes a report actionable.</summary>
    public const string Component = "Component";

    /// <summary>How long that component was broken, in seconds.</summary>
    public const string DegradedForSec = "DegradedForSec";

    /// <summary>What is taking the leaf down (<see cref="LeafStopReason"/>).</summary>
    public const string Reason = "Reason";

    /// <summary>How long the leaf ran, in seconds. Absent when the start could not be read.</summary>
    public const string UptimeSec = "UptimeSec";
}

/// <summary>
/// Why a leaf is stopping. Open to a leaf that means something else, but these three are the ones a
/// consumer distinguishes.
/// </summary>
public static class LeafStopReason
{
    /// <summary>SIGTERM or SIGINT — systemd stopping or restarting the unit, or somebody at a terminal.</summary>
    public const string Signal = "signal";

    /// <summary>
    /// A socket-activated leaf reached its idle window and gave its memory back.
    /// </summary>
    /// <remarks>
    /// Not a fault and not news: systemd holds the socket, and the next request starts it again. A
    /// consumer that alerts on a leaf going away must not alert on this one.
    /// </remarks>
    public const string Idle = "idle";

    /// <summary>
    /// The leaf is replacing itself in place, keeping what it supervises.
    /// </summary>
    /// <remarks>
    /// ⚠ The watchdog's hot-swap is this: SIGHUP then <c>execve</c>, same process id, and not one
    /// supervised game restarted. It looks like a stop and a start in the journal and it is neither,
    /// so a consumer that reports a restart has to read the reason before it does.
    /// </remarks>
    public const string Reload = "reload";
}
