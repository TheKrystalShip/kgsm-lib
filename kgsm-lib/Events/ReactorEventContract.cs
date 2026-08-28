namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// The event types the reactor leaf produces about what it judged.
/// </summary>
/// <remarks>
/// <para>
/// <b>A judgment, which exists nowhere else on the host.</b> The engine records what happened and the
/// supervisor records what it did about it; nobody records that a condition was read against a rule
/// and found to hold. That reading is the reactor's whole output — in observe mode it is its
/// <em>only</em> output — so an event is the sole way anything downstream learns a rule spoke.
/// </para>
/// <para>
/// <b>Two events, because a decision and an action are separate immutable facts.</b> Collapsing them
/// makes <em>"it decided and the action failed"</em> unrepresentable, which is exactly the case
/// somebody investigating an incident needs to see.
/// </para>
/// <para>
/// ⚠ <b>Written on a transition, never on an evaluation.</b> A state rule re-reads its condition on
/// every sweep and the reactor's ledger folds those into one row that gets better informed. Emitting
/// per evaluation would write a line every thirty seconds for a condition that has not changed, and
/// the journal would record how often the reactor looked rather than what it concluded.
/// </para>
/// </remarks>
public static class ReactorEvents
{
    /// <summary>
    /// <c>reactor.decided</c> — a rule reached a verdict about a subject.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Not an audit row.</b> A decision in observe mode is something this host <em>noticed</em>,
    /// not something it did, and an audit trail records what was performed. Only
    /// <see cref="Acted"/> has any business becoming one.
    /// </remarks>
    public const string Decided = "reactor.decided";

    /// <summary>
    /// <c>reactor.acted</c> — a decision was carried out, however it went.
    /// </summary>
    /// <remarks>
    /// It repeats the rule, the subject and the action rather than making a reader join back to
    /// <see cref="Decided"/> on the decision id: a consumer has to be able to render this from the one
    /// event, and a join is a second read that can fail while the first succeeded.
    /// </remarks>
    public const string Acted = "reactor.acted";

    /// <summary>
    /// The prefix every event this leaf writes shares.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The reactor tails every producer's journal, its own included</b>, so what it writes comes
    /// back to it. No rule may wake on one of these: a rule woken by a decision would decide about its
    /// own decision, write that, and be woken by it — at the sweep interval, forever, with a
    /// plausible-looking ledger. The leaf enforces it by construction, and this is the string anything
    /// else recognising the family reads.
    /// </remarks>
    public const string Prefix = "reactor.";
}

/// <summary>
/// The payload field names the reactor's events carry.
/// </summary>
/// <remarks>
/// Declared once, and read by all three descriptions of a field — what the leaf writes, what the
/// payload class binds to, and what the catalog classifies. Three descriptions are only ever the same
/// field if they are the same string.
/// </remarks>
public static class ReactorEventFields
{
    /// <summary>The rule that decided. Also the actor an audit row carries.</summary>
    public const string Rule = "Rule";

    /// <summary>Who had shaped that rule when it decided, as <c>provider:name</c>.</summary>
    public const string RuleAuthor = "RuleAuthor";

    /// <summary>What was judged — a server name, a sensor reference, a component.</summary>
    public const string Subject = "Subject";

    /// <summary>What sort of thing that is: <c>instance</c>, <c>host</c>, <c>leaf</c>, <c>unknown</c>.</summary>
    public const string SubjectKind = "SubjectKind";

    /// <summary>How loudly the rule speaks, in the ecosystem's severity spellings.</summary>
    public const string Severity = "Severity";

    /// <summary>The authority it ran under: <c>observe</c>, <c>propose</c>, <c>act</c>.</summary>
    public const string Mode = "Mode";

    /// <summary>What was decided — see <see cref="ReactorOutcomes"/>.</summary>
    public const string Outcome = "Outcome";

    /// <summary>Why, in one line. Always present.</summary>
    public const string Reason = "Reason";

    /// <summary>What the rule would do, as a stable name.</summary>
    public const string Action = "Action";

    /// <summary>The server the action operates on, or null when it operates on none.</summary>
    public const string ActionInstance = "ActionInstance";

    /// <summary>The decision's own identity, which <see cref="ReactorEvents.Acted"/> refers back to.</summary>
    public const string DecisionId = "DecisionId";

    /// <summary>When the condition opened. The envelope's timestamp is when it was decided.</summary>
    public const string OpenedAt = "OpenedAt";

    /// <summary>Whose journal the originating line is in.</summary>
    public const string SourceProducer = "SourceProducer";

    /// <summary>Which segment file of it.</summary>
    public const string SourceSegment = "SourceSegment";

    /// <summary>The byte offset in that segment.</summary>
    public const string SourceOffset = "SourceOffset";

    /// <summary>The id the originating line's producer minted for it, or null when it carries none.</summary>
    public const string SourceEventId = "SourceEventId";

    /// <summary>Whether the action succeeded.</summary>
    public const string Ok = "Ok";

    /// <summary>What the action produced — a backup id — or null.</summary>
    public const string Artifact = "Artifact";

    /// <summary>What went wrong, or what else is worth reading.</summary>
    public const string Detail = "Detail";
}

/// <summary>
/// What a rule concluded, and what the gate did with it.
/// </summary>
/// <remarks>
/// <para>
/// Lower case, which is the spelling every enumerated value in every event payload on this host uses
/// — a consumer compares against these without knowing anybody's casing convention.
/// </para>
/// <para>
/// ⚠ <b>Three of these are not verdicts about the world.</b> Fired and settled report what the rule
/// decided; suppressed, ceilinged and superseded report that it decided <em>yes</em> and the gate held
/// it back. A consumer counting "how often was this condition true" must count those alongside
/// <see cref="Fired"/>, and one counting "how often did anything happen" must not.
/// </para>
/// </remarks>
public static class ReactorOutcomes
{
    /// <summary>The condition held and nothing held the decision back.</summary>
    public const string Fired = "fired";

    /// <summary>The condition did not hold — usually because it resolved itself.</summary>
    public const string Settled = "settled";

    /// <summary>It held, and this rule had already spoken about this subject too recently.</summary>
    public const string Suppressed = "suppressed";

    /// <summary>It held, and the host-wide hourly ceiling was already reached.</summary>
    public const string Ceilinged = "ceilinged";

    /// <summary>It held, and a louder rule had already spoken for the same episode.</summary>
    public const string Superseded = "superseded";

    /// <summary>
    /// No judgment could be formed.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Never read as a no.</b> Either the world could not be read or what was read is not enough
    /// to decide on, and a surface that folded this into "the condition does not hold" would report
    /// silence as an all-clear.
    /// </remarks>
    public const string Unreadable = "unreadable";
}
