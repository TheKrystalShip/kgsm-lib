using System.Text.Json;
using System.Text.Json.Serialization;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// The root of every event data type — the emission metadata every KGSM event carries, independent of
/// what the event is ABOUT. Its subject-specific subclasses name that subject:
/// <see cref="EventDataBase"/> for instance-scoped events, <see cref="BlueprintEventDataBase"/> for
/// blueprint-scoped ones. A new kind of subject (host-scoped, leaf-scoped) adds one more sibling here
/// rather than borrowing a subject it does not have.
/// </summary>
public abstract class KgsmEventDataBase
{
    /// <summary>
    /// Gets or sets when the event was emitted (UTC). Populated from the event
    /// envelope's top-level <c>Timestamp</c> by <c>EventService</c>, not from the
    /// per-event <c>Data</c> payload. <see langword="null"/> means the emitter did
    /// not supply one (a pre-enrichment KGSM) — never a fabricated time.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets who triggered the event (the audit principal). Populated from
    /// the envelope's top-level <c>Actor</c> by <c>EventService</c>. KGSM takes it
    /// from <c>$KGSM_EVENT_ACTOR</c> (caller-supplied) or falls back to the invoking
    /// OS user; <see langword="null"/> means the emitter did not supply one.
    /// </summary>
    public string? Actor { get; set; }

    /// <summary>
    /// Gets or sets the surface that drove the event (<c>ui</c>, <c>assistant</c>,
    /// <c>discord</c>, <c>system</c>, <c>api</c>) — the companion to <see cref="Actor"/>
    /// (who) answering "through which surface". Populated from the envelope's top-level
    /// <c>Origin</c> by <c>EventService</c>. KGSM takes it from <c>$KGSM_EVENT_ORIGIN</c>
    /// (caller-supplied); unlike the actor there is no honest fallback for a bare CLI
    /// call, so <see langword="null"/> means no surface was declared — never fabricated.
    /// </summary>
    public string? Origin { get; set; }
}

/// <summary>
/// Base class for instance-scoped event data — every event whose subject is one game server instance.
/// The <see cref="InstanceName"/> identifies it.
/// </summary>
public abstract class EventDataBase : KgsmEventDataBase
{
    /// <summary>
    /// Gets or sets the name of the instance associated with the event.
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;
}

/// <summary>
/// Base class for blueprint-scoped event data — every event whose subject is a blueprint file rather
/// than an instance. Blueprints exist independently of any instance, so these carry a
/// <see cref="BlueprintName"/> where instance events carry an <c>InstanceName</c>.
/// </summary>
public abstract class BlueprintEventDataBase : KgsmEventDataBase
{
    /// <summary>
    /// Gets or sets the name of the blueprint the event is about (the file's basename without
    /// <c>.bp.yaml</c>).
    /// </summary>
    public string BlueprintName { get; set; } = string.Empty;
}

/// <summary>
/// Represents the wrapper for events received from KGSM — the top-level envelope
/// around each event's <see cref="Data"/> payload. Mirrors the wire shape emitted
/// by KGSM's <c>_build_event_payload</c>: <c>EventType</c>, <c>Data</c>, and the
/// emission metadata (<c>Timestamp</c>, <c>Actor</c>, <c>Origin</c>, <c>Hostname</c>,
/// <c>KGSMVersion</c>).
/// </summary>
public class EventWrapper
{
    /// <summary>
    /// Gets or sets the type of the event.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data associated with the event.
    /// </summary>
    public JsonElement Data { get; set; }

    /// <summary>
    /// Gets or sets when the event was emitted (UTC). <see langword="null"/> if the
    /// emitter did not include it.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets who triggered the event (the audit principal). <see langword="null"/>
    /// if the emitter did not include it.
    /// </summary>
    public string? Actor { get; set; }

    /// <summary>
    /// Gets or sets the surface that drove the event (<c>ui</c>, <c>assistant</c>,
    /// <c>discord</c>, <c>system</c>, <c>api</c>) — the companion to <see cref="Actor"/>.
    /// KGSM takes it from <c>$KGSM_EVENT_ORIGIN</c>; <see langword="null"/> when the
    /// emitter declared no surface (e.g. a bare CLI call) — never fabricated.
    /// </summary>
    public string? Origin { get; set; }

    /// <summary>
    /// Gets or sets the host KGSM emitted the event from. <see langword="null"/> if absent.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets the KGSM version that emitted the event. <see langword="null"/> if absent.
    /// </summary>
    [JsonPropertyName("KGSMVersion")]
    public string? KgsmVersion { get; set; }
}

/// <summary>
/// Event data for when an instance is created.
/// </summary>
public class InstanceCreatedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the blueprint name used to create the instance.
    /// </summary>
    public string Blueprint { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when instance directories are created.
/// </summary>
public class InstanceDirectoriesCreatedData : EventDataBase
{
}

/// <summary>
/// Event data for when instance files are created.
/// </summary>
public class InstanceFilesCreatedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance download starts.
/// </summary>
public class InstanceDownloadStartedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance download finishes.
/// </summary>
public class InstanceDownloadFinishedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance download fails.
/// </summary>
public class InstanceDownloadFailedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is downloaded.
/// </summary>
public class InstanceDownloadedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance deployment starts.
/// </summary>
public class InstanceDeployStartedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance deployment finishes.
/// </summary>
public class InstanceDeployFinishedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance deployment fails.
/// </summary>
public class InstanceDeployFailedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is deployed.
/// </summary>
public class InstanceDeployedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance's shutdown starts — the supervisor has asked the game to stop and
/// is waiting for it to drain. The matching <see cref="InstanceStopFinishedData"/> ends the run
/// whatever its outcome; <see cref="InstanceStoppedData"/> is the separate fact that the instance is
/// actually down.
/// </summary>
public class InstanceStopStartedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance's shutdown run ends. Emitted on every outcome — it states that
/// the run ENDED, not that the instance stopped (see <see cref="InstanceStoppedData"/>).
/// </summary>
public class InstanceStopFinishedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance update starts.
/// </summary>
public class InstanceUpdateStartedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance update finishes.
/// </summary>
public class InstanceUpdateFinishedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is updated.
/// </summary>
public class InstanceUpdatedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance version is updated.
/// </summary>
public class InstanceVersionUpdatedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the old version of the instance.
    /// </summary>
    public string OldVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the new version of the instance.
    /// </summary>
    public string NewVersion { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an instance installation starts.
/// </summary>
public class InstanceInstallationStartedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the blueprint name used for installation.
    /// </summary>
    public string Blueprint { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an instance installation finishes.
/// </summary>
public class InstanceInstallationFinishedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the blueprint name used for installation.
    /// </summary>
    public string Blueprint { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an instance is installed.
/// </summary>
public class InstanceInstalledData : EventDataBase
{
    /// <summary>
    /// Gets or sets the blueprint name used for installation.
    /// </summary>
    public string Blueprint { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an instance is started.
/// </summary>
public class InstanceStartedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is ready.
/// This event is triggered when the instance is fully operational and ready for use.
/// </summary>
public class InstanceReadyData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is stopped.
/// </summary>
public class InstanceStoppedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is restarted.
/// </summary>
public class InstanceRestartedData : EventDataBase
{
}

/// <summary>
/// Event data for when the resident supervisor (kgsm-watchdog) detected that an
/// instance's process died unexpectedly while it was desired-running and is
/// auto-restarting it. An autonomous engine action — emitted with
/// <c>Actor == "system"</c> and <c>Origin == "system"</c> (no human surface drove it).
/// Distinct from <see cref="InstanceRestartedData"/>, which is a deliberate operator
/// restart.
/// </summary>
public class InstanceCrashedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the leader process exit code the supervisor read, as a string
    /// (the wire value). The literal <c>"unknown"</c> when the code could not be read
    /// — never a fabricated code.
    /// </summary>
    public string ExitCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the consecutive restart-attempt count at the moment of the crash
    /// (always ≥ 1), as a string.
    /// </summary>
    public string Restarts { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when the resident supervisor (kgsm-watchdog) gave up auto-restarting
/// an instance after exhausting its restart retries; the instance is down and staying
/// down (supervision phase = failed). This is the escalation signal. An autonomous
/// engine action — emitted with <c>Actor == "system"</c> and <c>Origin == "system"</c>.
/// </summary>
public class InstanceFailedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the last leader process exit code the supervisor read, as a string
    /// (the wire value). The literal <c>"unknown"</c> when the code could not be read
    /// (e.g. the respawn itself failed to start) — never a fabricated code.
    /// </summary>
    public string ExitCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the consecutive-failure count at give-up (the retries exhausted),
    /// as a string.
    /// </summary>
    public string Restarts { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an instance backup is created.
/// </summary>
public class InstanceBackupCreatedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the source of the backup.
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the version of the backup.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an instance backup is restored.
/// </summary>
public class InstanceBackupRestoredData : EventDataBase
{
    /// <summary>
    /// Gets or sets the source of the backup that was restored.
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the version of the backup that was restored.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when instance files are removed.
/// </summary>
public class InstanceFilesRemovedData : EventDataBase
{
}

/// <summary>
/// Event data for when instance directories are removed.
/// </summary>
public class InstanceDirectoriesRemovedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is removed.
/// </summary>
public class InstanceRemovedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance uninstall starts.
/// </summary>
public class InstanceUninstallStartedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance uninstall finishes.
/// </summary>
public class InstanceUninstallFinishedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance uninstall fails.
/// </summary>
public class InstanceUninstallFailedData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance is uninstalled.
/// </summary>
public class InstanceUninstalledData : EventDataBase
{
}

/// <summary>
/// Event data for when an instance's host-firewall ports were opened via the
/// kgsm-firewall authority (firewall enable, or an install with firewall
/// management on). Only a confirmed open emits this — a down authority hard-fails
/// the action and emits nothing, so the event is never a fabricated outcome.
/// </summary>
public class InstancePortsOpenedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the ports that were opened, as the canonical range-preserving
    /// <see cref="PortMapping"/> array (the same shape <c>instances info --json</c>
    /// emits) — never an opaque UFW string.
    /// </summary>
    public List<PortMapping> Ports { get; set; } = [];
}

/// <summary>
/// Event data for when an instance's host-firewall ports were closed via the
/// kgsm-firewall authority (firewall disable, or uninstall). Only a confirmed
/// removal emits this — a down authority warns and emits nothing.
/// </summary>
public class InstancePortsClosedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the ports that were closed, as the canonical range-preserving
    /// <see cref="PortMapping"/> array — never an opaque UFW string.
    /// </summary>
    public List<PortMapping> Ports { get; set; } = [];
}

/// <summary>
/// Event data for when an instance's UPnP port mappings were opened on the local IGD
/// (router) — a <b>distinct</b> operation from the host-firewall
/// <see cref="InstancePortsOpenedData"/> (router NAT forward vs. ufw rule; a host can
/// have one without the other). Emitted by the kgsm-watchdog — the resident supervisor
/// owns UPnP because it is process-lifetime state — stamped <c>Actor == "system"</c> /
/// <c>Origin == "system"</c> (an autonomous daemon action). Only a confirmed mapping
/// (<c>upnpc</c> exited 0) emits this, never a fabricated outcome.
/// </summary>
public class InstanceUpnpOpenedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the ports that were forwarded, as the canonical range-preserving
    /// <see cref="PortMapping"/> array (the same shape <c>instances info --json</c>
    /// emits) — never an opaque UFW string.
    /// </summary>
    public List<PortMapping> Ports { get; set; } = [];
}

/// <summary>
/// Event data for when an instance's UPnP port mappings were closed on the local IGD
/// (router) — the close twin of <see cref="InstanceUpnpOpenedData"/>, distinct from the
/// host-firewall <see cref="InstancePortsClosedData"/>. Emitted by the kgsm-watchdog on
/// a deliberate stop, <c>Actor == "system"</c> / <c>Origin == "system"</c>. Only a
/// confirmed removal (<c>upnpc</c> exited 0) emits — a "nothing to delete" close (no
/// mapping existed) changes nothing and emits nothing.
/// </summary>
public class InstanceUpnpClosedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the ports whose forward was removed, as the canonical
    /// range-preserving <see cref="PortMapping"/> array — never an opaque UFW string.
    /// </summary>
    public List<PortMapping> Ports { get; set; } = [];
}

/// <summary>
/// Event data for when an instance's <c>.config.ini</c> had a single key changed
/// (via <c>kgsm config-set</c> / the lib's config setter). Both fields are always
/// present non-null strings — <see cref="EventDataBase.InstanceName"/> identifies the
/// instance and <see cref="Key"/> names the changed key. The new <em>value</em> is
/// intentionally never carried (secret hygiene — instance config can hold passwords
/// or tokens), so a consumer auditing this only learns that <see cref="Key"/> changed,
/// never to what.
/// </summary>
public class InstanceConfigChangedData : EventDataBase
{
    /// <summary>The config key that was changed. The value is intentionally never carried
    /// (secret hygiene — instance config can hold passwords/tokens).</summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when an arbitrary console command was sent to a running instance
/// (via <c>kgsm instances input</c> / the lib's <c>IInstanceService.SendInput</c>).
/// <see cref="EventDataBase.InstanceName"/> identifies the instance and <see cref="Command"/>
/// is the verbatim command text delivered to the server's console input.
/// </summary>
/// <remarks>
/// Unlike <see cref="InstanceConfigChangedData"/> (which carries the key but never the value),
/// this event carries the FULL command text — a deliberate choice so the audit trail records
/// exactly what an operator ran (console commands are admin-level: ban/kick/op/…). A console
/// command can therefore contain a secret (e.g. an RCON login); the trade was accepted because
/// the command surface is operator-gated and the trail's value is who-ran-what. A consumer that
/// must redact should do so at its own boundary.
/// </remarks>
public class InstanceInputSentData : EventDataBase
{
    /// <summary>The verbatim console command that was sent to the instance.</summary>
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// Event data for when a player joined a running instance. For our kgsm-containers
/// images these are forwarded by the kgsm-watchdog — it tails the in-container event
/// channel and re-emits via kgsm-lib — stamped <c>Actor == "system"</c> /
/// <c>Origin == "system"</c> (an autonomous observation, not a human action). Native
/// detection (log-scraping via <c>NativeLogMatcher</c>) is the same shape. At least one
/// of <see cref="PlayerId"/> / <see cref="PlayerName"/> / <see cref="PlayerAddr"/> is
/// non-null (the emitting side enforces it — a session with no human-meaningful field
/// is not a roster entry).
/// </summary>
public class InstancePlayerJoinedData : EventDataBase
{
    /// <summary>
    /// Gets or sets the opaque, game-scoped stable player id (SteamID64 / Minecraft
    /// UUID / …) when the source provides one, otherwise <see langword="null"/>.
    /// Never fabricated — a source that gives only a display name leaves this null.
    /// </summary>
    public string? PlayerId { get; set; }

    /// <summary>
    /// Gets or sets the player's display label when the source provides one, otherwise
    /// <see langword="null"/>. Never fabricated. See <see cref="PlayerId"/> for the
    /// at-least-one-non-null guarantee.
    /// </summary>
    public string? PlayerName { get; set; }

    /// <summary>
    /// Gets or sets the player's real network address (<c>ip:port</c>) when the source
    /// exposes one (direct-socket games), otherwise <see langword="null"/>. Steam-relay
    /// / P2P games never expose a real address — never fabricated.
    /// </summary>
    public string? PlayerAddr { get; set; }

    /// <summary>
    /// Gets or sets the opaque per-session correlation token (<c>key ?? addr ?? id ??
    /// name</c>) — always a non-empty string. Not an identity, not an address; the
    /// roster of record (kgsm-api) keys on this rather than on the display name (names
    /// can collide across sessions).
    /// </summary>
    public string? SessionKey { get; set; }
}

/// <summary>
/// Event data for when a player left a running instance. The leave counterpart of
/// <see cref="InstancePlayerJoinedData"/> — same source, provenance, and nullable
/// identity rules.
/// </summary>
public class InstancePlayerLeftData : EventDataBase
{
    /// <summary>
    /// Gets or sets the opaque, game-scoped stable player id (SteamID64 / Minecraft
    /// UUID / …) when the source provides one, otherwise <see langword="null"/>.
    /// Never fabricated.
    /// </summary>
    public string? PlayerId { get; set; }

    /// <summary>
    /// Gets or sets the player's display label when the source provides one, otherwise
    /// <see langword="null"/>. Never fabricated. See <see cref="PlayerId"/> for the
    /// at-least-one-non-null guarantee.
    /// </summary>
    public string? PlayerName { get; set; }

    /// <summary>
    /// Gets or sets the player's real network address (<c>ip:port</c>) when the source
    /// exposes one (direct-socket games), otherwise <see langword="null"/>. Never
    /// fabricated.
    /// </summary>
    public string? PlayerAddr { get; set; }

    /// <summary>
    /// Gets or sets the opaque per-session correlation token (<c>key ?? addr ?? id ??
    /// name</c>) — always a non-empty string. Matches the token captured on this
    /// session's join, letting a consumer correlate the pair without touching
    /// display-name identity (which can collide).
    /// </summary>
    public string? SessionKey { get; set; }

    /// <summary>
    /// Gets or sets the disconnect reason when the game's log carries one (e.g.
    /// <c>RemoteConnectionClose</c>, <c>App_Min</c>), otherwise <see langword="null"/>.
    /// Never fabricated. Kick/ban classification of this vocabulary is deferred to a
    /// future version.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Base for the player-moderation audit events — an operator removed a player from a
/// running instance, blocked them, or lifted that block.
/// </summary>
/// <remarks>
/// These are their own event types rather than an <see cref="InstanceInputSentData"/>
/// record because the subject is a <em>player</em>, not a command: a consumer asking
/// "who was banned on this server" filters on the event type instead of pattern-matching
/// command text — text a hand-typed <c>instances input</c> could also produce, with no
/// moderation intent behind it.
/// </remarks>
public abstract class InstanceModerationDataBase : EventDataBase
{
    /// <summary>
    /// Gets or sets the player identity the operator supplied — whichever kind the
    /// game's blueprint template declared (<c>{ip}</c>, <c>{name}</c> or <c>{id}</c>).
    /// Carried verbatim and never classified here: the blueprint is where that meaning
    /// is declared, and re-deriving it would be a second answer that could disagree.
    /// A consumer that needs the kind reads it from the instance's template with
    /// <see cref="Core.Models.ModerationCommand.TryGetTargetKind"/>.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resolved console command that was delivered, so the trail
    /// records the literal effect beside its subject.
    /// </summary>
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// Data for the <c>instance_player_kicked</c> event — a player was disconnected from a
/// running instance. Emitted only once the command has been delivered.
/// </summary>
public class InstancePlayerKickedData : InstanceModerationDataBase
{
}

/// <summary>
/// Data for the <c>instance_player_banned</c> event — a player was disconnected and
/// blocked from reconnecting. Emitted only once the command has been delivered.
/// </summary>
public class InstancePlayerBannedData : InstanceModerationDataBase
{
}

/// <summary>
/// Data for the <c>instance_player_unbanned</c> event — a block was lifted. Emitted only
/// once the command has been delivered.
/// </summary>
/// <remarks>
/// The subject of an unban is by definition not connected, so this event is the record a
/// consumer offering "lift a ban" selects from — a live player roster cannot supply it.
/// </remarks>
public class InstancePlayerUnbannedData : InstanceModerationDataBase
{
}

/// <summary>
/// Data for the <c>blueprint_created</c> event — a blueprint file was written under a name that had no
/// user file before. The blueprint's CONTENT is deliberately absent: no event payload ever carries a file
/// body or a diff.
/// </summary>
public class BlueprintCreatedData : BlueprintEventDataBase
{
    /// <summary>
    /// Gets or sets which directory the file was written to. Always
    /// <see cref="BlueprintTier.User"/> — the system directory is never written to.
    /// </summary>
    public BlueprintTier Tier { get; set; }

    /// <summary>
    /// Gets or sets whether the new file shadows a same-named shipped blueprint.
    /// <see langword="null"/> when the emitter could not determine it — never a defaulted false.
    /// </summary>
    public bool? OverridesSystem { get; set; }

    /// <summary>
    /// Gets or sets the runtime the written blueprint declares (<c>native</c>/<c>container</c>).
    /// <see langword="null"/> when the emitter could not read one out of the file.
    /// </summary>
    public string? Runtime { get; set; }
}

/// <summary>
/// Data for the <c>blueprint_updated</c> event — an existing user blueprint file was overwritten. Carries
/// no content or diff, for the same reason as <see cref="BlueprintCreatedData"/>.
/// </summary>
public class BlueprintUpdatedData : BlueprintEventDataBase
{
    /// <summary>
    /// Gets or sets which directory the file was written to. Always
    /// <see cref="BlueprintTier.User"/>.
    /// </summary>
    public BlueprintTier Tier { get; set; }

    /// <summary>
    /// Gets or sets whether the file shadows a same-named shipped blueprint.
    /// <see langword="null"/> when the emitter could not determine it.
    /// </summary>
    public bool? OverridesSystem { get; set; }

    /// <summary>
    /// Gets or sets the runtime the written blueprint declares (<c>native</c>/<c>container</c>).
    /// <see langword="null"/> when the emitter could not read one out of the file.
    /// </summary>
    public string? Runtime { get; set; }
}

/// <summary>
/// Data for the <c>blueprint_removed</c> event — a user blueprint file was deleted.
/// </summary>
public class BlueprintRemovedData : BlueprintEventDataBase
{
    /// <summary>
    /// Gets or sets which directory the file was deleted from. Always
    /// <see cref="BlueprintTier.User"/> — a shipped blueprint can never be removed.
    /// </summary>
    public BlueprintTier Tier { get; set; }

    /// <summary>
    /// Gets or sets whether a shipped original is now in effect again. <see langword="true"/> means the
    /// removal reverted an override; <see langword="false"/> means the blueprint is gone entirely.
    /// <see langword="null"/> when the emitter could not determine it.
    /// </summary>
    public bool? RevertedToSystem { get; set; }
}
