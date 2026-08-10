using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

// DTOs for the kgsm-watchdog control surface (HTTP/1.1 over a unix socket). These
// mirror the daemon's own contracts (kgsm-watchdog: src/Watchdog/Model/Contracts.cs)
// one-for-one. The daemon serializes camelCase; every property below carries an
// explicit [JsonPropertyName] so binding is exact regardless of any naming policy,
// and registration in KgsmJsonContext keeps deserialization reflection-free
// (Native-AOT/trim-safe).

/// <summary>
/// Result of a watchdog control action (<c>start</c>/<c>stop</c>): which instance,
/// whether it succeeded, and a human-readable reason.
/// </summary>
public record class WatchdogActionResult
{
    /// <summary>The instance the action targeted.</summary>
    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Whether the action succeeded. False is returned (with HTTP 409) when the
    /// instance was already in the requested state, alongside an explanatory
    /// <see cref="Message"/> — it is not an exception, just a no-op outcome.
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    /// <summary>Human-readable detail describing the outcome.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Reported state of a supervised instance: the <em>desired</em> state the daemon
/// holds vs. the <em>actual</em> liveness measured from <c>cgroup.events</c>, plus
/// the supervision phase and current restart-failure streak. Never fabricated —
/// <see cref="Populated"/> is read from the kernel.
/// </summary>
public record class WatchdogInstanceState
{
    /// <summary>The instance name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Desired state held by the daemon: <c>"running"</c> or <c>"stopped"</c>.</summary>
    [JsonPropertyName("desired")]
    public string Desired { get; set; } = string.Empty;

    /// <summary>Actual liveness, measured from the instance's <c>cgroup.events</c>.</summary>
    [JsonPropertyName("populated")]
    public bool Populated { get; set; }

    /// <summary>The spawned leader PID, when known; null otherwise.</summary>
    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    /// <summary>The instance's cgroup path under the delegated slice.</summary>
    [JsonPropertyName("cgroupPath")]
    public string CgroupPath { get; set; } = string.Empty;

    /// <summary>
    /// Supervision phase: <c>"running"</c>, <c>"restart-pending"</c>,
    /// <c>"stopped"</c>, <c>"failed"</c>, or <c>"unknown"</c>.
    /// </summary>
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    /// <summary>Consecutive-failure streak since last stability (0 when healthy).</summary>
    [JsonPropertyName("restarts")]
    public int Restarts { get; set; }

    /// <summary>Last transition reason (e.g. <c>"crashed (exit 139); restart in 2s"</c>).</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Readiness of the supervisor itself: whether it is in-slice and able to spawn.
/// Distinct from process liveness — the daemon can be up but unable to supervise.
/// </summary>
public record class WatchdogReadyState
{
    /// <summary>True when the supervisor is in-slice and able to spawn.</summary>
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    /// <summary>Human-readable detail (the precise reason when not ready).</summary>
    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// One UPnP port-mapping row the local IGD holds for an instance, mirroring the daemon's
/// <c>UpnpMapping</c> (kgsm-watchdog: <c>GET /upnp/{name}</c>). Measured from the router, never
/// fabricated — the mapping's <see cref="Description"/> equals the instance name (the ownership tag the
/// watchdog sets when it opens the forward).
/// </summary>
public record class WatchdogUpnpMapping
{
    /// <summary>The external (WAN-side) port the router forwards.</summary>
    [JsonPropertyName("externalPort")]
    public int ExternalPort { get; set; }

    /// <summary>Transport protocol — <c>"tcp"</c> or <c>"udp"</c>.</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>The internal (LAN-side) port the forward targets.</summary>
    [JsonPropertyName("internalPort")]
    public int InternalPort { get; set; }

    /// <summary>The internal (LAN-side) client address the forward targets.</summary>
    [JsonPropertyName("internalClient")]
    public string InternalClient { get; set; } = string.Empty;

    /// <summary>The mapping description — equals the owning instance's name (the ownership tag).</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// An instance's current UPnP mappings on the local IGD (kgsm-watchdog: <c>GET /upnp/{name}</c>).
/// <see cref="State"/> is load-bearing for honesty: <c>"queried"</c> means the router was asked and
/// <see cref="Mappings"/> is what it owns (possibly empty — a real "none"); <c>"unavailable"</c> means
/// the router could not be asked at all (upnpc missing, no IGD, or a timeout) and is NEVER "no mappings".
/// This in-body <c>"unavailable"</c> (the daemon is reachable, the router is not) is distinct from a null
/// return (the daemon itself unreachable).
/// </summary>
public record class WatchdogUpnpList
{
    /// <summary>The instance the mappings belong to.</summary>
    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;

    /// <summary>Query state: <c>"queried"</c> (asked the router) or <c>"unavailable"</c> (couldn't).</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>The mappings the router owns for this instance; empty when queried-and-none.</summary>
    [JsonPropertyName("mappings")]
    public List<WatchdogUpnpMapping> Mappings { get; set; } = [];
}

/// <summary>
/// A single player session tracked by the watchdog's in-memory session map. Served by
/// <c>GET /players</c> so consumers (kgsm-api) can reconcile their roster on startup.
/// </summary>
public record class WatchdogPlayer
{
    /// <summary>The session key (first non-blank of key, addr, id, name — contract §4).</summary>
    [JsonPropertyName("sessionKey")]
    public string? SessionKey { get; set; }

    /// <summary>The player's account or platform id, when known.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The player's display name, when known.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The player's network address, when known.</summary>
    [JsonPropertyName("addr")]
    public string? Addr { get; set; }
}

/// <summary>
/// One instance's player presence: whether the supervisor can observe it, and who it currently
/// sees connected. Served by <c>GET /players</c>, one entry per instance.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Detection"/> is what makes <see cref="Players"/> readable, and the pair is why this
/// is one type.</b> An empty list under <c>log</c> or <c>rcon</c> means nobody is connected — a
/// measured fact. An empty list under <c>none</c> means the game reports nothing, and a surface that
/// renders it as "0 online" states something the host does not know. A consumer cannot take the
/// list without the qualifier because they arrive together.
/// </para>
/// <para>
/// <b>The supervisor decides this, not the consumer.</b> The predicate behind it includes whether a
/// pattern <i>compiles</i>, so re-deriving it from an instance's config is something no surface can
/// get right — and three surfaces each deriving it is how they come to disagree. Use
/// <see cref="IsDetected"/> rather than comparing the string.
/// </para>
/// </remarks>
public record class WatchdogInstancePresence
{
    /// <summary>
    /// How presence is observed: <c>log</c> (matched from the game's output — real transitions),
    /// <c>rcon</c> (polled and diffed — cannot see churn between polls), <c>none</c> (not observable
    /// at all), or <c>unknown</c> (the supervisor could not read the instance inventory, so the
    /// capability could not be established either way).
    /// </summary>
    [JsonPropertyName("detection")]
    public string Detection { get; set; } = "unknown";

    /// <summary>
    /// The sessions currently tracked. <b>Empty means "nobody" only when <see cref="IsDetected"/>
    /// is true</b>; otherwise it means nobody can tell.
    /// </summary>
    [JsonPropertyName("players")]
    public List<WatchdogPlayer> Players { get; set; } = [];

    /// <summary>
    /// Whether this instance's roster is a measurement. False for both <c>none</c> and
    /// <c>unknown</c> — a capability that could not be established is not one that exists, and the
    /// honest answer to "who is online" is the same in both cases.
    /// </summary>
    [JsonIgnore]
    public bool IsDetected =>
        Detection is "log" or "rcon";
}
