using System.Text.Json;
using System.Text.Json.Serialization;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// Base class for all event data types.
/// This class contains common properties that all event data will inherit.
/// All events have an InstanceName property to identify the instance they are related to.
/// </summary>
public abstract class EventDataBase
{
    /// <summary>
    /// Gets or sets the name of the instance associated with the event.
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

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
/// Event data for when a player joined a running instance. For our kgsm-containers
/// images these are forwarded by the kgsm-watchdog — it tails the in-container event
/// channel and re-emits via kgsm-lib — stamped <c>Actor == "system"</c> /
/// <c>Origin == "system"</c> (an autonomous observation, not a human action). Native
/// detection is a later increment. At least one of <see cref="PlayerId"/> /
/// <see cref="PlayerName"/> is non-null (the emitting shim enforces it).
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
}
