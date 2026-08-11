namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// What each engine event <em>is</em> — its subject, whether it reports a fact or a step inside one,
/// whether it reports a failure, and what kind of data each of its payload fields holds.
/// </summary>
/// <remarks>
/// <para>
/// The companion to <see cref="Services.EventService"/>'s type map. That one says which class an event
/// deserializes into; this says what a consumer needs to know to handle it without having worked out
/// the answer for itself. Every surface that renders the journal was deriving this independently,
/// which is how two of them came to disagree about whether a player's network address may be shown.
/// </para>
/// <para>
/// <b>This states facts and never policy.</b> <see cref="EventWeight.Phase"/> does not mean "hide
/// this" and <see cref="FieldSensitivity.Personal"/> does not mean "refuse this" — a consumer decides
/// what to do with a fact, and two consumers are allowed to decide differently. The moment this
/// carries a permission, the surfaces lose the right to differ and every one of them inherits
/// whichever surface wrote the rule.
/// </para>
/// <para>
/// Static data, no reflection: this library is embedded by Native-AOT consumers. The tests that keep
/// it honest — every typed event described, every declared payload property classified — do use
/// reflection, and are not AOT.
/// </para>
/// </remarks>
public static class KgsmEventCatalog
{
    /// <summary>
    /// What is known about <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    /// <b>Never null.</b> An event type this build has never heard of gets a descriptor with
    /// <see cref="EventDescriptor.Known"/> false, so a consumer always has something to work from and
    /// never has to decide whether the lookup failed or the answer was "nothing". See
    /// <see cref="EventDescriptor.Known"/> for what such a descriptor does and does not claim.
    /// </remarks>
    public static EventDescriptor Describe(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Descriptors.TryGetValue(type, out EventDescriptor? known) ? known : Unrecognized(type);
    }

    /// <summary>Every event type this build classifies, in no particular order.</summary>
    public static IReadOnlyCollection<EventDescriptor> All => Descriptors.Values;

    /// <summary>
    /// A descriptor for a type nobody has classified.
    /// </summary>
    /// <remarks>
    /// The subject is read off the engine's own naming convention (<c>&lt;subject&gt;_&lt;verb&gt;</c>)
    /// rather than left unanswered: every consumer would otherwise write that same derivation itself,
    /// which is the duplication this catalog exists to remove. Everything else is the cautious answer —
    /// a fact, no outcome claimed, and <b>no fields</b>, which means "render nothing out of the
    /// payload", not "the payload is empty". That is deliberate: an unclassified event may carry
    /// anything, and a consumer that prints unclassified fields is how a credential reaches a channel.
    /// </remarks>
    private static EventDescriptor Unrecognized(string type) => new(
        Type: type,
        Subject: type.StartsWith("blueprint_", StringComparison.Ordinal)
            ? EventSubject.Blueprint
            : EventSubject.Instance,
        Weight: EventWeight.Fact,
        Outcome: EventOutcome.Neutral,
        Fields: [],
        Known: false);

    // ---- the classification -------------------------------------------------------------------
    //
    // Grouped the way the engine emits them. A multi-step operation brackets its work with
    // <thing>_started / <thing>_finished around the one event that IS the news — those brackets are
    // Phase, the news is Fact, and a failure is always Fact because a step that did not happen is
    // exactly what somebody reading back needs to find.

    private static readonly Dictionary<string, EventDescriptor> Descriptors;

    // Built in a static constructor rather than a field initializer, and it has to be: the shared
    // field definitions below are static initializers themselves, and those run in textual order —
    // building the table from a field initializer would read every one of them before it was assigned.
    // A static constructor body runs after all of them, whatever order the file is in.
    static KgsmEventCatalog() => Descriptors = Build();

    private static Dictionary<string, EventDescriptor> Build()
    {
        var all = new List<EventDescriptor>
        {
            // -- install -----------------------------------------------------------------------
            Instance("instance_created", EventWeight.Phase, fields: [Blueprint]),
            Instance("instance_directories_created", EventWeight.Phase),
            Instance("instance_files_created", EventWeight.Phase),
            Instance("instance_download_started", EventWeight.Phase),
            Instance("instance_download_finished", EventWeight.Phase),
            Instance("instance_download_failed", EventWeight.Fact, EventOutcome.Failure),
            Instance("instance_downloaded", EventWeight.Phase),
            Instance("instance_deploy_started", EventWeight.Phase),
            Instance("instance_deploy_finished", EventWeight.Phase),
            Instance("instance_deploy_failed", EventWeight.Fact, EventOutcome.Failure),
            Instance("instance_deployed", EventWeight.Phase),
            Instance("instance_installation_started", EventWeight.Phase, fields: [Blueprint]),
            Instance("instance_installation_finished", EventWeight.Phase, fields: [Blueprint]),
            Instance("instance_installed", EventWeight.Fact, EventOutcome.Success, [Blueprint]),

            // -- uninstall ---------------------------------------------------------------------
            Instance("instance_uninstall_started", EventWeight.Phase),
            Instance("instance_uninstall_finished", EventWeight.Phase),
            Instance("instance_uninstall_failed", EventWeight.Fact, EventOutcome.Failure),
            Instance("instance_uninstalled", EventWeight.Fact, EventOutcome.Success),
            Instance("instance_files_removed", EventWeight.Phase),
            Instance("instance_directories_removed", EventWeight.Phase),
            Instance("instance_removed", EventWeight.Phase),

            // -- run state ---------------------------------------------------------------------
            Instance("instance_started", EventWeight.Fact),

            // The moment players can actually connect, which is not what instance_started reports —
            // that one says the process launched. Two facts about two different moments.
            Instance("instance_ready", EventWeight.Fact, EventOutcome.Success),

            Instance("instance_stopped", EventWeight.Fact),
            Instance("instance_stop_started", EventWeight.Phase),
            Instance("instance_stop_finished", EventWeight.Phase),
            Instance("instance_restarted", EventWeight.Fact),
            Instance("instance_restart_started", EventWeight.Phase),
            Instance("instance_restart_finished", EventWeight.Phase),
            Instance("instance_crashed", EventWeight.Fact, EventOutcome.Failure, [ExitCode, Restarts]),
            Instance("instance_failed", EventWeight.Fact, EventOutcome.Failure, [ExitCode, Restarts]),

            // -- versions ----------------------------------------------------------------------
            Instance("instance_update_started", EventWeight.Phase),
            Instance("instance_update_finished", EventWeight.Phase),

            // The update run ended; whether the version moved is instance_version_updated's to say.
            Instance("instance_updated", EventWeight.Phase),

            Instance("instance_update_available", EventWeight.Fact, EventOutcome.Neutral,
                [Field("CurrentVersion", FieldShape.Version), Field("LatestVersion", FieldShape.Version)]),
            Instance("instance_version_updated", EventWeight.Fact, EventOutcome.Success,
                [Field("OldVersion", FieldShape.Version), Field("NewVersion", FieldShape.Version)]),

            // -- backups -----------------------------------------------------------------------
            Instance("instance_backup_created", EventWeight.Fact, EventOutcome.Success, [Source, Version]),
            Instance("instance_backup_restored", EventWeight.Fact, EventOutcome.Success, [Source, Version]),
            Instance("instance_backup_deleted", EventWeight.Fact, EventOutcome.Neutral, [Source]),
            Instance("instance_backups_pruned", EventWeight.Fact, EventOutcome.Neutral,
                [Field("Deleted", FieldShape.Number), Field("Kept", FieldShape.Number)]),

            // -- the doors ---------------------------------------------------------------------
            // A host firewall rule and a router NAT forward are different facts about different
            // machines, and both bracket a run rather than stepping through one — so both are facts.
            Instance("instance_ports_opened", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance("instance_ports_closed", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance("instance_upnp_opened", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance("instance_upnp_closed", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance("instance_upnp_reasserted", EventWeight.Fact, EventOutcome.Neutral, [Ports]),

            // -- players -----------------------------------------------------------------------
            Instance("instance_player_joined", EventWeight.Fact, EventOutcome.Neutral,
                [PlayerId, PlayerName, PlayerAddr, SessionKey]),
            Instance("instance_player_left", EventWeight.Fact, EventOutcome.Neutral,
                [PlayerId, PlayerName, PlayerAddr, SessionKey, Field("Reason", FieldShape.Text)]),

            Instance("instance_player_kicked", EventWeight.Fact, EventOutcome.Neutral, [Target, Command]),
            Instance("instance_player_banned", EventWeight.Fact, EventOutcome.Neutral, [Target, Command]),
            Instance("instance_player_unbanned", EventWeight.Fact, EventOutcome.Neutral, [Target, Command]),

            // -- operator actions --------------------------------------------------------------
            // The key only: kgsm deliberately never puts the value on the event, because a config
            // value can be an rcon password.
            Instance("instance_config_changed", EventWeight.Fact, EventOutcome.Neutral,
                [Field("Key", FieldShape.Text)]),

            Instance("instance_input_sent", EventWeight.Fact, EventOutcome.Neutral, [Command]),

            // -- blueprints --------------------------------------------------------------------
            BlueprintEvent("blueprint_created", [Tier, OverridesSystem, Runtime]),
            BlueprintEvent("blueprint_updated", [Tier, OverridesSystem, Runtime]),
            BlueprintEvent("blueprint_removed", [Tier, Field("RevertedToSystem", FieldShape.Text)]),
        };

        var byType = new Dictionary<string, EventDescriptor>(all.Count, StringComparer.Ordinal);
        foreach (EventDescriptor descriptor in all)
            byType.Add(descriptor.Type, descriptor);

        return byType;
    }

    private static EventDescriptor Instance(
        string type,
        EventWeight weight,
        EventOutcome outcome = EventOutcome.Neutral,
        IReadOnlyList<EventField>? fields = null) =>
        new(type, EventSubject.Instance, weight, outcome, fields ?? [], Known: true);

    private static EventDescriptor BlueprintEvent(string type, IReadOnlyList<EventField> fields) =>
        new(type, EventSubject.Blueprint, EventWeight.Fact, EventOutcome.Neutral, fields, Known: true);

    private static EventField Field(
        string name, FieldShape shape, FieldSensitivity sensitivity = FieldSensitivity.Public) =>
        new(name, sensitivity, shape);

    // The fields that appear on more than one event, declared once so two events cannot classify the
    // same field differently.
    private static readonly EventField Blueprint = Field("Blueprint", FieldShape.Text);
    private static readonly EventField Source = Field("Source", FieldShape.Text);
    private static readonly EventField Version = Field("Version", FieldShape.Version);
    private static readonly EventField ExitCode = Field("ExitCode", FieldShape.Text);
    private static readonly EventField Restarts = Field("Restarts", FieldShape.Number);
    private static readonly EventField Ports = Field("Ports", FieldShape.Ports);
    private static readonly EventField Tier = Field("Tier", FieldShape.Text);
    private static readonly EventField Runtime = Field("Runtime", FieldShape.Text);
    private static readonly EventField OverridesSystem = Field("OverridesSystem", FieldShape.Text);

    /// <summary>
    /// The name and id a game shows other players in its own scoreboard. Public because that is what
    /// they already are — a roster that refused them would answer nobody's question.
    /// </summary>
    private static readonly EventField PlayerName = Field("PlayerName", FieldShape.Identity);
    private static readonly EventField PlayerId = Field("PlayerId", FieldShape.Identity);

    /// <summary>
    /// Where somebody connected from. It identifies a person rather than a player, and the game shows
    /// it to nobody.
    /// </summary>
    private static readonly EventField PlayerAddr =
        Field("PlayerAddr", FieldShape.Identity, FieldSensitivity.Personal);

    /// <summary>
    /// The supervisor's correlation token for one session. Public — it says nothing about anybody —
    /// but <see cref="FieldShape.Opaque"/>, so nothing renders it for want of meaning rather than for
    /// privacy.
    /// </summary>
    private static readonly EventField SessionKey = Field("SessionKey", FieldShape.Opaque);

    /// <summary>
    /// Who a moderation action named. <see cref="FieldSensitivity.Conditional"/> because the value may
    /// be an address, a name or an id and <em>the event does not say which</em> — the game's blueprint
    /// declares it. This library already refuses to classify that here, because re-deriving it would be
    /// a second answer that could disagree with the template; the catalog refuses for the same reason
    /// rather than guessing on its behalf.
    /// </summary>
    private static readonly EventField Target =
        Field("Target", FieldShape.Identity, FieldSensitivity.Conditional);

    /// <summary>
    /// The console command that was delivered. Admin-level by nature — a command can create an
    /// operator, set a password or carry a token — so it is privileged rather than public, whatever
    /// the particular command turns out to be.
    /// </summary>
    private static readonly EventField Command =
        Field("Command", FieldShape.Text, FieldSensitivity.Privileged);
}

/// <summary>
/// What is known about one engine event type.
/// </summary>
/// <param name="Type">The raw engine event type, e.g. <c>instance_started</c>.</param>
/// <param name="Subject">What the event is about.</param>
/// <param name="Weight">Whether it reports a fact or a step inside one.</param>
/// <param name="Outcome">Whether it reports something completing or failing.</param>
/// <param name="Fields">
/// The payload's own fields and what each holds. <b>Empty means "render nothing from the payload"</b>,
/// which is not the same as the payload being empty — an event whose fields nobody has classified may
/// carry anything.
/// </param>
/// <param name="Known">
/// Whether this build classifies the type at all. On <see langword="false"/> the only load-bearing
/// values are <paramref name="Type"/> and the empty <paramref name="Fields"/>; the subject is read off
/// the engine's naming convention and the rest are cautious defaults. An unknown type must still be
/// shown — dropping it is how a surface reports a busy night as a quiet one.
/// </param>
public sealed record EventDescriptor(
    string Type,
    EventSubject Subject,
    EventWeight Weight,
    EventOutcome Outcome,
    IReadOnlyList<EventField> Fields,
    bool Known)
{
    /// <summary>The classification of <paramref name="name"/>, or null if the event has no such field.</summary>
    public EventField? Field(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));
}

/// <summary>One payload field: what it is called, what kind of thing it holds, and how careful to be with it.</summary>
/// <param name="Name">The field's name on the event's data class and in its JSON payload.</param>
/// <param name="Sensitivity">What kind of data it is. Never who may see it — that is a surface's decision.</param>
/// <param name="Shape">What it looks like, so a renderer knows whether it can be printed as a scalar.</param>
public sealed record EventField(string Name, FieldSensitivity Sensitivity, FieldShape Shape);

/// <summary>What an event is about.</summary>
public enum EventSubject
{
    /// <summary>One game server instance; the payload names it.</summary>
    Instance,

    /// <summary>One blueprint — a template, not an installed server. Never read as being about an instance.</summary>
    Blueprint,
}

/// <summary>
/// Whether an event is the news or part of getting to it.
/// </summary>
public enum EventWeight
{
    /// <summary>Something happened that stands on its own.</summary>
    Fact,

    /// <summary>
    /// A step inside a multi-step operation that has its own <see cref="Fact"/> event — the brackets
    /// around an install, a stop, an update. A surface that lists everything reads as noise; a surface
    /// tracking progress needs exactly these. Which is why this classifies rather than prescribes.
    /// </summary>
    Phase,
}

/// <summary>
/// Whether an event reports something completing or failing.
/// </summary>
/// <remarks>
/// <b>That</b> an event reports a failure is the engine's word; <b>how loudly</b> a surface says so is
/// that surface's business. This is the first half only — there is deliberately no severity here.
/// </remarks>
public enum EventOutcome
{
    /// <summary>An observation or a state change; nothing completed or failed.</summary>
    Neutral,

    /// <summary>A multi-step operation reports it finished, and did what it set out to.</summary>
    Success,

    /// <summary>Something reports it did not happen, or stopped happening.</summary>
    Failure,
}

/// <summary>
/// What kind of data a field holds. A statement about the data, never about who may see it.
/// </summary>
public enum FieldSensitivity
{
    /// <summary>Safe wherever events are read at all.</summary>
    Public,

    /// <summary>
    /// May identify a person, depending on something the event does not carry. A consumer that cannot
    /// resolve which it is must treat it as <see cref="Personal"/>.
    /// </summary>
    Conditional,

    /// <summary>Identifies a natural person rather than a player.</summary>
    Personal,

    /// <summary>Operator-level content that may carry a credential.</summary>
    Privileged,
}

/// <summary>What a field looks like, so a renderer knows what it can do with it.</summary>
public enum FieldShape
{
    /// <summary>A short string fit to print.</summary>
    Text,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A game or build version string.</summary>
    Version,

    /// <summary>Something that names a player.</summary>
    Identity,

    /// <summary>The canonical port list — structured, and never flattened by a generic renderer.</summary>
    Ports,

    /// <summary>A moment in time.</summary>
    Timestamp,

    /// <summary>A token with no meaning to a reader. Carried for correlation, never displayed.</summary>
    Opaque,
}
