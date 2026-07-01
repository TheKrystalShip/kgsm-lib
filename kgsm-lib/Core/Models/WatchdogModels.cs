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
