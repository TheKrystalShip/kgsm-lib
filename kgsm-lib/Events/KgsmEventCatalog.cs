using TheKrystalShip.KGSM.Lifecycle;

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
        PayloadType: null,
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
            Instance<InstanceCreatedData>("instance_created", EventWeight.Phase, fields: [Blueprint]),
            Instance<InstanceDirectoriesCreatedData>("instance_directories_created", EventWeight.Phase),
            Instance<InstanceFilesCreatedData>("instance_files_created", EventWeight.Phase),
            Instance<InstanceDownloadStartedData>("instance_download_started", EventWeight.Phase),
            Instance<InstanceDownloadFinishedData>("instance_download_finished", EventWeight.Phase),
            Instance<InstanceDownloadFailedData>("instance_download_failed", EventWeight.Fact, EventOutcome.Failure),
            Instance<InstanceDownloadedData>("instance_downloaded", EventWeight.Phase),
            Instance<InstanceDeployStartedData>("instance_deploy_started", EventWeight.Phase),
            Instance<InstanceDeployFinishedData>("instance_deploy_finished", EventWeight.Phase),
            Instance<InstanceDeployFailedData>("instance_deploy_failed", EventWeight.Fact, EventOutcome.Failure),
            Instance<InstanceDeployedData>("instance_deployed", EventWeight.Phase),
            Instance<InstanceInstallationStartedData>("instance_installation_started", EventWeight.Phase, fields: [Blueprint]),
            Instance<InstanceInstallationFinishedData>("instance_installation_finished", EventWeight.Phase, fields: [Blueprint]),
            Instance<InstanceInstalledData>("instance_installed", EventWeight.Fact, EventOutcome.Success, [Blueprint]),

            // -- uninstall ---------------------------------------------------------------------
            Instance<InstanceUninstallStartedData>("instance_uninstall_started", EventWeight.Phase),
            Instance<InstanceUninstallFinishedData>("instance_uninstall_finished", EventWeight.Phase),
            Instance<InstanceUninstallFailedData>("instance_uninstall_failed", EventWeight.Fact, EventOutcome.Failure),
            Instance<InstanceUninstalledData>("instance_uninstalled", EventWeight.Fact, EventOutcome.Success),
            Instance<InstanceFilesRemovedData>("instance_files_removed", EventWeight.Phase),
            Instance<InstanceDirectoriesRemovedData>("instance_directories_removed", EventWeight.Phase),
            Instance<InstanceRemovedData>("instance_removed", EventWeight.Phase),

            // -- run state ---------------------------------------------------------------------
            Instance<InstanceStartedData>("instance_started", EventWeight.Fact),

            // The moment players can actually connect, which is not what instance_started reports —
            // that one says the process launched. Two facts about two different moments.
            Instance<InstanceReadyData>("instance_ready", EventWeight.Fact, EventOutcome.Success),

            Instance<InstanceStoppedData>("instance_stopped", EventWeight.Fact),
            Instance<InstanceStopStartedData>("instance_stop_started", EventWeight.Phase),
            Instance<InstanceStopFinishedData>("instance_stop_finished", EventWeight.Phase),
            Instance<InstanceRestartedData>("instance_restarted", EventWeight.Fact),
            Instance<InstanceRestartStartedData>("instance_restart_started", EventWeight.Phase),
            Instance<InstanceRestartStoppedData>("instance_restart_stopped", EventWeight.Phase),
            Instance<InstanceRestartFinishedData>("instance_restart_finished", EventWeight.Phase),
            Instance<InstanceCrashedData>("instance_crashed", EventWeight.Fact, EventOutcome.Failure, [ExitCode, Restarts]),
            Instance<InstanceFailedData>("instance_failed", EventWeight.Fact, EventOutcome.Failure, [ExitCode, Restarts]),

            // -- versions ----------------------------------------------------------------------
            Instance<InstanceUpdateStartedData>("instance_update_started", EventWeight.Phase),
            Instance<InstanceUpdateFinishedData>("instance_update_finished", EventWeight.Phase),

            // The update run ended; whether the version moved is instance_version_updated's to say.
            Instance<InstanceUpdatedData>("instance_updated", EventWeight.Phase),

            // The run ended WITHOUT the version moving, for a reason. Without it, a failed update and
            // a successful one that found nothing to do are the same two bracket lines — and since the
            // bracket alone is what a consumer settles a run on, a refused update reads as a completed
            // one. A Fact, like every other failure: a step that did not happen is exactly what
            // somebody reading back needs to find.
            Instance<InstanceUpdateFailedData>("instance_update_failed", EventWeight.Fact, EventOutcome.Failure),

            Instance<InstanceUpdateAvailableData>("instance_update_available", EventWeight.Fact, EventOutcome.Neutral,
                [Field("CurrentVersion", FieldShape.Version), Field("LatestVersion", FieldShape.Version)]),
            Instance<InstanceVersionUpdatedData>("instance_version_updated", EventWeight.Fact, EventOutcome.Success,
                [Field("OldVersion", FieldShape.Version), Field("NewVersion", FieldShape.Version)]),

            // -- backups -----------------------------------------------------------------------
            // Both verbs are minutes of archiving on a large world, and a scheduler drives them
            // unattended — so each brackets its run the way the lifecycle verbs do, and a surface can
            // show the instance as busy for the whole of it rather than learning at the end.
            Instance<InstanceBackupStartedData>("instance_backup_started", EventWeight.Phase),
            Instance<InstanceBackupFinishedData>("instance_backup_finished", EventWeight.Phase),
            Instance<InstanceRestoreStartedData>("instance_restore_started", EventWeight.Phase),
            Instance<InstanceRestoreFinishedData>("instance_restore_finished", EventWeight.Phase),
            Instance<InstanceBackupCreatedData>("instance_backup_created", EventWeight.Fact, EventOutcome.Success, [Source, Version]),
            Instance<InstanceBackupRestoredData>("instance_backup_restored", EventWeight.Fact, EventOutcome.Success, [Source, Version]),
            Instance<InstanceBackupDeletedData>("instance_backup_deleted", EventWeight.Fact, EventOutcome.Neutral, [Source]),
            Instance<InstanceBackupsPrunedData>("instance_backups_pruned", EventWeight.Fact, EventOutcome.Neutral,
                [Field("Deleted", FieldShape.Number), Field("Kept", FieldShape.Number)]),

            // -- the doors ---------------------------------------------------------------------
            // A host firewall rule and a router NAT forward are different facts about different
            // machines, and both bracket a run rather than stepping through one — so both are facts.
            Instance<InstancePortsOpenedData>("instance_ports_opened", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance<InstancePortsClosedData>("instance_ports_closed", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance<InstanceUpnpOpenedData>("instance_upnp_opened", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance<InstanceUpnpClosedData>("instance_upnp_closed", EventWeight.Fact, EventOutcome.Neutral, [Ports]),
            Instance<InstanceUpnpReassertedData>("instance_upnp_reasserted", EventWeight.Fact, EventOutcome.Neutral, [Ports]),

            // -- host monitoring ---------------------------------------------------------------
            // A breach and a recovery are two immutable facts, not one row that changes: the journal is
            // append-only, and the live view of the same condition is the alert feed, which answers a
            // different question. Neither is a Failure — a value crossing a line is a measurement, and
            // how loudly to say so is the reading surface's business.
            Host<HostThresholdBreachedData>("host_threshold_breached", EventOutcome.Neutral,
                [Field("EpisodeId", FieldShape.Opaque), Field("RuleKey", FieldShape.Text),
                 Field("Metric", FieldShape.Text), Field("Scope", FieldShape.Text),
                 Field("Ref", FieldShape.Text), Field("ServerId", FieldShape.Text),
                 Field("Threshold", FieldShape.Number), Field("PeakValue", FieldShape.Number),
                 Field("PeakBand", FieldShape.Text), Field("OpenedTs", FieldShape.Number),
                 Field("OpenValue", FieldShape.Number), Field("Band", FieldShape.Text)]),

            Host<HostThresholdClearedData>("host_threshold_cleared", EventOutcome.Neutral,
                [Field("EpisodeId", FieldShape.Opaque), Field("RuleKey", FieldShape.Text),
                 Field("Metric", FieldShape.Text), Field("Scope", FieldShape.Text),
                 Field("Ref", FieldShape.Text), Field("ServerId", FieldShape.Text),
                 Field("Threshold", FieldShape.Number), Field("PeakValue", FieldShape.Number),
                 Field("PeakBand", FieldShape.Text), Field("OpenedTs", FieldShape.Number),
                 Field("ClosedTs", FieldShape.Number), Field("CloseValue", FieldShape.Number),
                 // ⚠ Not always a recovery. A rule retuned, disabled or removed closes an episode
                 // without the value ever being observed to come down.
                 Field("CloseReason", FieldShape.Text)]),

            // -- players -----------------------------------------------------------------------
            Instance<InstancePlayerJoinedData>("instance_player_joined", EventWeight.Fact, EventOutcome.Neutral,
                [PlayerId, PlayerName, PlayerAddr, SessionKey]),
            Instance<InstancePlayerLeftData>("instance_player_left", EventWeight.Fact, EventOutcome.Neutral,
                [PlayerId, PlayerName, PlayerAddr, SessionKey, Field("Reason", FieldShape.Text)]),

            Instance<InstancePlayerKickedData>("instance_player_kicked", EventWeight.Fact, EventOutcome.Neutral, [Target, Command]),
            Instance<InstancePlayerBannedData>("instance_player_banned", EventWeight.Fact, EventOutcome.Neutral, [Target, Command]),
            Instance<InstancePlayerUnbannedData>("instance_player_unbanned", EventWeight.Fact, EventOutcome.Neutral, [Target, Command]),

            // -- operator actions --------------------------------------------------------------
            // The key only: kgsm deliberately never puts the value on the event, because a config
            // value can be an rcon password.
            Instance<InstanceConfigChangedData>("instance_config_changed", EventWeight.Fact, EventOutcome.Neutral,
                [Field("Key", FieldShape.Text)]),

            Instance<InstanceInputSentData>("instance_input_sent", EventWeight.Fact, EventOutcome.Neutral, [Command]),

            // -- blueprints --------------------------------------------------------------------
            BlueprintEvent<BlueprintCreatedData>("blueprint_created", [Tier, OverridesSystem, Runtime]),
            BlueprintEvent<BlueprintUpdatedData>("blueprint_updated", [Tier, OverridesSystem, Runtime]),
            BlueprintEvent<BlueprintRemovedData>("blueprint_removed", [Tier, Field("RevertedToSystem", FieldShape.Text)]),

            // -- accounts ----------------------------------------------------------------------
            // Signing in and authority changing. The Control Panel performs these itself — no engine
            // command runs — so it authors them, and they are classified here because a payload field
            // nobody has classified renders nowhere and these carry the values most worth care.
            Account<AuthSessionEventData>("auth_login", EventOutcome.Success, SessionFields),
            Account<AuthSessionEventData>("auth_logout", EventOutcome.Neutral, SessionFields),

            // A peer node asserting an already-authenticated identity, which this host then mints its
            // own session for. Same shape as a login because that is what it is; PeerNode is what says
            // the proof was somebody else's.
            Account<AuthSessionEventData>("auth_cluster_session", EventOutcome.Success, SessionFields),

            Account<AuthSessionRevokedData>("auth_session_revoked", EventOutcome.Neutral,
                [UserId, Username, Field("Scope", FieldShape.Text), Sid, Field("Count", FieldShape.Number)]),

            // An account's authority is only ever changed here — the store is the sole authority on
            // this host — so these six are the whole record of anybody's permissions moving.
            Account<UserAccountEventData>("user_provisioned", EventOutcome.Neutral, AccountChangeFields),
            Account<UserAccountEventData>("user_approved", EventOutcome.Success, AccountChangeFields),
            Account<UserAccountEventData>("user_disabled", EventOutcome.Neutral, AccountChangeFields),
            Account<UserAccountEventData>("user_tier_changed", EventOutcome.Neutral, AccountChangeFields),
            Account<UserAccountEventData>("user_deleted", EventOutcome.Neutral, AccountChangeFields),

            // ⚠ Records that a credential was set and by whom. Never the credential.
            Account<UserAccountEventData>("user_password_changed", EventOutcome.Neutral, AccountChangeFields),

            Account<IdentityLinkEventData>("identity_linked", EventOutcome.Neutral, IdentityFields),
            Account<IdentityLinkEventData>("identity_unlinked", EventOutcome.Neutral, IdentityFields),

            // -- leaf services -----------------------------------------------------------------
            Service<ServiceProvisioningEventData>("service_connected", EventOutcome.Success, [Leaf, DisplayName]),
            Service<ServiceProvisioningEventData>("service_disconnected", EventOutcome.Neutral, [Leaf, DisplayName]),

            // Keys only. A leaf's configuration holds tokens and passwords, so the value a change set
            // is not part of the fact that it changed.
            Service<ServiceConfigChangedEventData>("service_config_changed", EventOutcome.Neutral,
                [Leaf, DisplayName, Field("Keys", FieldShape.Text), Field("Outcome", FieldShape.Text)]),

            Service<ServiceRestartedEventData>("service_restarted", EventOutcome.Neutral,
                [Leaf, DisplayName, Field("Unit", FieldShape.Text), Ok]),

            // -- what a leaf says about ITSELF --------------------------------------------------
            // Same subject as the four above and the opposite direction: those record what kgsm-api
            // did to a leaf on somebody's instruction, these are the leaf's own report. ⚠ None of
            // them names a leaf, because the journal a line was read from already does — see
            // LeafLifecycleEventData.
            LeafEvent<LeafReadyEventData>(LeafLifecycleEvents.Ready, EventOutcome.Success,
                [StartupMs, Detail]),

            LeafEvent<LeafDegradedEventData>(LeafLifecycleEvents.Degraded, EventOutcome.Failure,
                [Component, Detail]),

            LeafEvent<LeafRecoveredEventData>(LeafLifecycleEvents.Recovered, EventOutcome.Success,
                [Component, DegradedForSec]),

            // Neutral, not Failure: a leaf saying it is going away on purpose is the fact that
            // separates a deploy from an outage, and reporting it as a failure would lose exactly the
            // distinction it exists to make.
            LeafEvent<LeafStoppingEventData>(LeafLifecycleEvents.Stopping, EventOutcome.Neutral,
                [Reason, UptimeSec]),

            // -- what the assistant says about its own conduct ----------------------------------
            // Service-subject: none of them is an event about a game server, because on every one of
            // them nothing happened to a server. Filing a refusal or an unapproved proposal under the
            // instance it names would say the opposite of what it records.
            AssistantEvent<AssistantClaimCorrectedEventData>(
                AssistantEvents.ClaimCorrected, EventOutcome.Failure,
                [Check, Resolution, Net, ConversationId]),

            AssistantEvent<AssistantActionDeclinedEventData>(
                AssistantEvents.ActionDeclined, EventOutcome.Failure,
                [Tool, DeclineReason, Tier, ActionInstance]),

            // Neutral: a proposal is the assistant doing exactly what it is meant to — stopping to ask.
            AssistantEvent<AssistantActionProposedEventData>(
                AssistantEvents.ActionProposed, EventOutcome.Neutral,
                [Kind, Tool, ActionInstance, ExpiresInSec]),

            // Blueprint-subject, and the only pair here that brackets rather than reports: the engine
            // records the probe install and uninstall in full, and these say the rows belong together.
            new(AssistantEvents.BlueprintAuthoringStarted, EventSubject.Blueprint, EventWeight.Phase,
                EventOutcome.Neutral, [Probe],
                typeof(AssistantBlueprintAuthoringStartedEventData), Known: true),

            new(AssistantEvents.BlueprintAuthored, EventSubject.Blueprint, EventWeight.Fact,
                EventOutcome.Neutral, [Probe, AuthoringOutcome, DurationSec],
                typeof(AssistantBlueprintAuthoredEventData), Known: true),

            // -- panel actions on an instance --------------------------------------------------
            // Instance-subject because that is what they are about, even though the Control Panel and
            // not the engine performed them. ⚠ Both carry an identity of the bytes and never the bytes:
            // an instance config file holds rcon passwords, and a world is somebody's data.
            Instance<FileWrittenEventData>("file_written", EventWeight.Fact, EventOutcome.Neutral,
                [Path, SizeBytes, Sha256]),
            Instance<BackupDownloadedEventData>("backup_downloaded", EventWeight.Fact, EventOutcome.Neutral,
                [Field("BackupId", FieldShape.Text), SizeBytes, Sha256]),
        };

        var byType = new Dictionary<string, EventDescriptor>(all.Count, StringComparer.Ordinal);
        foreach (EventDescriptor descriptor in all)
            byType.Add(descriptor.Type, descriptor);

        return byType;
    }

    /// <summary>
    /// One instance-subject event, named together with the class its payload deserializes into.
    /// </summary>
    /// <remarks>
    /// <b>The type parameter is what makes the classification and the dispatch one registry.</b>
    /// <see cref="Services.EventService"/> reads <see cref="EventDescriptor.PayloadType"/> to decide
    /// what to deserialize, so an event cannot be dispatched without being described — that is now
    /// true by construction rather than by a test comparing two lists. Its constraint carries a second
    /// guarantee for free: an instance-subject event must have an instance-shaped payload, so the
    /// subject and the payload's own base cannot disagree.
    /// </remarks>
    private static EventDescriptor Instance<TData>(
        string type,
        EventWeight weight,
        EventOutcome outcome = EventOutcome.Neutral,
        IReadOnlyList<EventField>? fields = null)
        where TData : EventDataBase =>
        new(type, EventSubject.Instance, weight, outcome, fields ?? [], typeof(TData), Known: true);

    /// <summary>The blueprint-subject counterpart, constrained to the sibling payload base.</summary>
    /// <summary>A host-scoped descriptor — a fact this machine's own monitoring established.</summary>
    private static EventDescriptor Host<TData>(
        string type,
        EventOutcome outcome,
        IReadOnlyList<EventField> fields)
        where TData : HostThresholdEventDataBase =>
        new(type, EventSubject.Host, EventWeight.Fact, outcome, fields, typeof(TData), Known: true);

    private static EventDescriptor BlueprintEvent<TData>(string type, IReadOnlyList<EventField> fields)
        where TData : BlueprintEventDataBase =>
        new(type, EventSubject.Blueprint, EventWeight.Fact, EventOutcome.Neutral, fields,
            typeof(TData), Known: true);

    /// <summary>An account-subject descriptor — something that happened to somebody's access.</summary>
    private static EventDescriptor Account<TData>(
        string type, EventOutcome outcome, IReadOnlyList<EventField> fields)
        where TData : AccountEventDataBase =>
        new(type, EventSubject.Account, EventWeight.Fact, outcome, fields, typeof(TData), Known: true);

    /// <summary>A leaf-service descriptor.</summary>
    private static EventDescriptor Service<TData>(
        string type, EventOutcome outcome, IReadOnlyList<EventField> fields)
        where TData : ServiceEventData =>
        new(type, EventSubject.Service, EventWeight.Fact, outcome, fields, typeof(TData), Known: true);

    /// <summary>
    /// A descriptor for a leaf's report about itself.
    /// </summary>
    /// <remarks>
    /// The same subject as <see cref="Service{TData}"/> — it is still an event about a leaf service —
    /// with the payload constraint that keeps the two apart. A self-reported event must not carry a
    /// leaf id, and <see cref="ServiceEventData"/> requires one.
    /// </remarks>
    private static EventDescriptor LeafEvent<TData>(
        string type, EventOutcome outcome, IReadOnlyList<EventField> fields)
        where TData : LeafLifecycleEventData =>
        new(type, EventSubject.Service, EventWeight.Fact, outcome, fields, typeof(TData), Known: true);

    /// <summary>
    /// A descriptor for the assistant's report about its own conduct.
    /// </summary>
    /// <remarks>
    /// Service-subject, and constrained to a payload that cannot name a leaf, for the same reason
    /// <see cref="LeafEvent{TData}"/> is: the journal directory already says who produced it.
    /// </remarks>
    private static EventDescriptor AssistantEvent<TData>(
        string type, EventOutcome outcome, IReadOnlyList<EventField> fields)
        where TData : AssistantEventData =>
        new(type, EventSubject.Service, EventWeight.Fact, outcome, fields, typeof(TData), Known: true);

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
    /// The fields a leaf's own lifecycle events carry, named from the emitter's constants rather than
    /// from a string here. Three descriptions of one field — what the leaf writes, what the payload
    /// class binds to, and what this classifies — are only ever the same field if they are the same
    /// string.
    /// </summary>
    private static readonly EventField StartupMs = Field(LeafLifecycleFields.StartupMs, FieldShape.Number);
    private static readonly EventField Detail = Field(LeafLifecycleFields.Detail, FieldShape.Text);
    private static readonly EventField Component = Field(LeafLifecycleFields.Component, FieldShape.Text);
    private static readonly EventField DegradedForSec = Field(LeafLifecycleFields.DegradedForSec, FieldShape.Number);
    private static readonly EventField Reason = Field(LeafLifecycleFields.Reason, FieldShape.Text);
    private static readonly EventField UptimeSec = Field(LeafLifecycleFields.UptimeSec, FieldShape.Number);

    /// <summary>
    /// The fields the assistant's own events carry, named from the contract's constants for the same
    /// reason the lifecycle ones are — three descriptions of one field are only the same field if they
    /// are the same string.
    /// </summary>
    private static readonly EventField Check = Field(AssistantEventFields.Check, FieldShape.Text);
    private static readonly EventField Resolution = Field(AssistantEventFields.Resolution, FieldShape.Text);
    private static readonly EventField Net = Field(AssistantEventFields.Net, FieldShape.Text);
    private static readonly EventField Tool = Field(AssistantEventFields.Tool, FieldShape.Text);
    private static readonly EventField DeclineReason = Field(AssistantEventFields.DeclineReason, FieldShape.Text);
    private static readonly EventField ActionInstance = Field(AssistantEventFields.Instance, FieldShape.Text);
    private static readonly EventField Kind = Field(AssistantEventFields.Kind, FieldShape.Text);
    private static readonly EventField ExpiresInSec = Field(AssistantEventFields.ExpiresInSec, FieldShape.Number);
    private static readonly EventField Probe = Field(AssistantEventFields.Probe, FieldShape.Text);
    private static readonly EventField AuthoringOutcome =
        Field(AssistantEventFields.AuthoringOutcome, FieldShape.Text);
    private static readonly EventField DurationSec = Field(AssistantEventFields.DurationSec, FieldShape.Number);

    /// <summary>
    /// The conversation a claim was corrected in. <see cref="FieldShape.Opaque"/> because it means
    /// nothing to a reader, and <see cref="FieldSensitivity.Personal"/> because the key embeds the
    /// account it belongs to — it is a correlation token that happens to name somebody.
    /// </summary>
    private static readonly EventField ConversationId =
        Field(AssistantEventFields.ConversationId, FieldShape.Opaque, FieldSensitivity.Personal);

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

    // ---- the Control Panel's own fields --------------------------------------------------------

    /// <summary>
    /// The account's stable id. Public and <see cref="FieldShape.Opaque"/>: it is a generated key that
    /// says nothing about anybody on its own, and nothing should render it for meaning.
    /// </summary>
    private static readonly EventField UserId = Field("UserId", FieldShape.Opaque);

    /// <summary>
    /// What the account was called when this happened. Public — it is already the name every audit row
    /// carries as its actor, and a trail that hid it would name nobody.
    /// </summary>
    private static readonly EventField Username = Field("Username", FieldShape.Identity);

    /// <summary>
    /// The external handle an account signs in with, as <c>provider:name</c>.
    /// <see cref="FieldSensitivity.Personal"/>: unlike the username, it links this host's account to a
    /// person's identity somewhere else, and it is the account's own holder who chose to attach it —
    /// not something the panel gets to publish to everyone who can read the log.
    /// </summary>
    private static readonly EventField Identity =
        Field("Identity", FieldShape.Identity, FieldSensitivity.Personal);

    /// <summary>Which provider vouched. Public — naming the door is not naming who came through it.</summary>
    private static readonly EventField Provider = Field("Provider", FieldShape.Text);

    /// <summary>
    /// The session id, so a login and its logout pair up. Public and opaque, on the same reasoning as
    /// <see cref="SessionKey"/> — it correlates two rows and grants nothing.
    /// </summary>
    private static readonly EventField Sid = Field("Sid", FieldShape.Opaque);

    /// <summary>
    /// The calling device. <see cref="FieldSensitivity.Personal"/> for the same reason
    /// <see cref="PlayerAddr"/> is: a user-agent string describes somebody's machine, and it is on the
    /// row to answer "was that me?" for the account's holder, not to tell a reader what everyone else
    /// browses with.
    /// </summary>
    private static readonly EventField UserAgent =
        Field("UserAgent", FieldShape.Text, FieldSensitivity.Personal);

    private static readonly EventField Leaf = Field("Leaf", FieldShape.Text);
    private static readonly EventField DisplayName = Field("DisplayName", FieldShape.Text);
    private static readonly EventField Ok = Field("Ok", FieldShape.Text);
    private static readonly EventField Path = Field("Path", FieldShape.Text);
    private static readonly EventField SizeBytes = Field("SizeBytes", FieldShape.Number);

    /// <summary>The content hash. Identifies the bytes; is not the bytes.</summary>
    private static readonly EventField Sha256 = Field("Sha256", FieldShape.Opaque);

    /// <summary>The fields every <c>auth_*</c> session event carries.</summary>
    private static readonly EventField[] SessionFields =
        [UserId, Username, Identity, Provider, Tier, Sid, UserAgent, Field("PeerNode", FieldShape.Text)];

    /// <summary>The fields every <c>user_*</c> account-change event carries.</summary>
    private static readonly EventField[] AccountChangeFields =
    [
        UserId, Username,
        Field("FromTier", FieldShape.Text), Field("ToTier", FieldShape.Text),
        Field("FromStatus", FieldShape.Text), Field("ToStatus", FieldShape.Text),
        Field("ByHolder", FieldShape.Text),
    ];

    /// <summary>The fields both <c>identity_*</c> events carry.</summary>
    private static readonly EventField[] IdentityFields =
        [UserId, Username, Provider, Field("Handle", FieldShape.Identity, FieldSensitivity.Personal)];
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
/// <param name="PayloadType">
/// The class this event's <c>Data</c> deserializes into, and the reason the catalog is the <em>only</em>
/// registry of what the engine emits: <see cref="Services.EventService"/> dispatches off this, so an
/// event that can be deserialized is necessarily one that has been classified. Null exactly when
/// <paramref name="Known"/> is false — nothing can be deserialized for a type this build has never
/// heard of, which is why an unknown event reaches a handler only as a raw envelope.
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
    System.Type? PayloadType,
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

    /// <summary>
    /// The host itself — a fact this machine's monitoring established, which may name a server it is
    /// about without being an event that server produced.
    /// </summary>
    Host,

    /// <summary>
    /// One KGSM account — who signed in, whose authority changed, which identity was attached. The
    /// subject is the account rather than the person, because an account is what authority resolves
    /// against and it outlives any name somebody is currently shown under.
    /// </summary>
    Account,

    /// <summary>
    /// One leaf service on this host — connected, disconnected, reconfigured, restarted. Never read as
    /// being about a game server: a leaf can be reconfigured while every instance keeps running.
    /// </summary>
    Service,
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
