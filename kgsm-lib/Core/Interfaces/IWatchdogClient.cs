using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Typed client for the kgsm-watchdog control surface — the single C# entry point
/// for driving the resident supervisor daemon over its unix socket. Surfaces above
/// (the Discord bot, a future CLI/web BFF) issue lifecycle commands through this
/// instead of shelling out, keeping all watchdog integration in kgsm-lib.
/// </summary>
/// <remarks>
/// Transport is HTTP/1.1 over a unix-domain socket. The socket's filesystem
/// permissions are the security boundary; there is no in-band authentication
/// (that belongs to the network-facing surfaces above). The action methods throw
/// <see cref="System.Net.Http.HttpRequestException"/> when the daemon is
/// unreachable; <see cref="IsReadyAsync"/> instead reports <c>false</c>, so it can
/// be used as a presence probe.
/// </remarks>
public interface IWatchdogClient : IDisposable
{
    /// <summary>
    /// Probes supervisor readiness via the unified <c>GET /health</c> probe. Returns
    /// <c>true</c> only when the daemon answers 200 (in-slice and able to spawn); any
    /// other status (503 + reason) or an unreachable/stale socket yields <c>false</c>
    /// rather than throwing, so this doubles as a presence check.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the supervisor readiness detail (the <c>/health</c> body, served on
    /// both 200 and 503). Returns <c>null</c> if the daemon is unreachable.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchdogReadyState?> GetReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the daemon spawn and supervise <paramref name="instanceName"/>
    /// (records desired-state = running). An already-running instance returns a
    /// result with <see cref="WatchdogActionResult.Ok"/> = false rather than
    /// throwing.
    /// </summary>
    /// <param name="instanceName">The instance to start.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchdogActionResult> StartAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the daemon stop <paramref name="instanceName"/> (records
    /// desired-state = stopped, so it will not be crash-restarted) and perform the
    /// graceful drain → <c>cgroup.kill</c> teardown.
    /// </summary>
    /// <param name="instanceName">The instance to stop.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchdogActionResult> StopAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="instanceName"/> to the watchdog's persisted boot-autostart set so the
    /// daemon will spawn it automatically on the next host boot (or watchdog start). Idempotent —
    /// already-enabled returns <see cref="WatchdogActionResult.Ok"/> = false (409) rather than throwing.
    /// </summary>
    Task<WatchdogActionResult> EnableAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes <paramref name="instanceName"/> from the watchdog's persisted boot-autostart set.
    /// Idempotent — already-disabled returns <see cref="WatchdogActionResult.Ok"/> = false (409) rather than throwing.
    /// </summary>
    Task<WatchdogActionResult> DisableAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the names of all instances currently in the persisted boot-autostart set.
    /// An empty list means no instances are enabled (never null).
    /// </summary>
    Task<IReadOnlyList<string>> GetEnabledNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters <paramref name="instanceName"/> from the watchdog entirely — the supervision table
    /// entry, its cgroup, its boot-autostart intent, and its persisted restart counters. The counterpart
    /// to an uninstall: an instance that no longer exists must stop being supervised, or the daemon holds
    /// a <c>desired=running</c> record forever and every consumer of its state keeps seeing a condition
    /// for a server that is gone.
    /// <para>
    /// Idempotent and existence-free — an unknown name returns <see cref="WatchdogActionResult.Ok"/> =
    /// true as a no-op, since the instance's kgsm spec is normally already deleted by the time this is
    /// called. <see cref="WatchdogActionResult.Ok"/> = false (409) means the instance is still running
    /// and was NOT deregistered (deregistering it would orphan the process).
    /// </para>
    /// </summary>
    Task<WatchdogActionResult> ForgetAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Live-applies a CPU scheduling priority to the running instance's cgroup.
    /// Translates the priority string to a cgroup <c>cpu.weight</c> value
    /// (low=50, normal=100, high=400) and writes it. Returns <c>Ok=false</c>
    /// with a message (not an exception) if the instance cgroup does not exist
    /// (not running) — the caller should treat this as "will apply at next start".
    /// </summary>
    Task<WatchdogActionResult> SetCpuPriorityAsync(string instanceName, string priority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the daemon atomically restart <paramref name="instanceName"/>:
    /// stops the process, waits for the cgroup to drain, then respawns it.
    /// Does not increment the crash-recovery streak (this is an intentional restart).
    /// The <paramref name="origin"/> names the REQUESTING LEAF, and the daemon attributes the
    /// emitted audit event to it — as <c>system:&lt;origin&gt;</c>, the actor form a consumer reads
    /// as an autonomous leaf rather than as a person on the local host. The event's own origin is
    /// <c>system</c>, since a leaf-driven restart has no human surface behind it.
    /// </summary>
    /// <param name="instanceName">The instance to restart.</param>
    /// <param name="origin">The requesting leaf, e.g. <c>"scheduler"</c> (the default).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchdogActionResult> RestartAsync(
        string instanceName,
        string origin = "scheduler",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supervised state of a single instance, or <c>null</c> when the
    /// daemon does not track it (HTTP 404).
    /// </summary>
    /// <param name="instanceName">The instance to query.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchdogInstanceState?> GetStatusAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every instance the daemon currently supervises.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<WatchdogInstanceState>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Follows the live console (stdout/stderr tail) of a native, supervised instance,
    /// yielding each line as the daemon appends it. The stream carries <b>only</b> lines
    /// written <em>after</em> the call connects — it does not replay history (use
    /// <see cref="GetConsoleTailAsync"/> for a backlog).
    /// </summary>
    /// <remarks>
    /// The stream is <b>unbounded and never self-completes on the daemon's own initiative</b>:
    /// it does not end on the instance stopping, on log EOF, or on a missing-on-disk log (a
    /// missing log holds the connection open, polling, until the file appears). In normal
    /// operation enumeration ends only when the caller cancels
    /// <paramref name="cancellationToken"/> (or disposes the enumerator) — at which point an
    /// <see cref="OperationCanceledException"/> may surface from the iterator. A
    /// <em>server-side</em> disconnect (the daemon stopping, or the socket dropping) appears
    /// as stream EOF and ends the sequence <b>normally</b> (no exception) — a consumer that
    /// wants to keep following must re-invoke; the sequence ending is not by itself proof the
    /// caller cancelled. An unknown / non-native / no-console instance yields an empty
    /// sequence (the daemon answers 404 before the first byte). The shared request timeout
    /// does <b>not</b> apply to this call — it streams for as long as the caller keeps the
    /// token un-cancelled and the connection stays up.
    /// </remarks>
    /// <param name="instanceName">The instance whose console to follow.</param>
    /// <param name="cancellationToken">Stops the follow when cancelled — the normal way it ends (a server-side disconnect ends it without cancellation).</param>
    /// <returns>An async sequence of console lines (newline already stripped).</returns>
    IAsyncEnumerable<string> FollowConsoleAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a finite tail of a native, supervised instance's console — the last
    /// <paramref name="lines"/> lines currently on disk, oldest-first.
    /// </summary>
    /// <remarks>
    /// An unknown / non-native / no-console instance (the daemon answers 404) returns an
    /// <b>empty</b> list rather than throwing — an honest "no console" read, mirroring how
    /// <see cref="GetStatusAsync"/> degrades a 404 to null. An instance with a console but
    /// no lines also returns an empty list. The daemon clamps <paramref name="lines"/> to
    /// its own bounds (0..5000); a request transport failure still throws.
    /// </remarks>
    /// <param name="instanceName">The instance whose console tail to read.</param>
    /// <param name="lines">How many trailing lines to request (the daemon clamps 0..5000).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The trailing console lines oldest-first, or an empty list when there is no console.</returns>
    Task<IReadOnlyList<string>> GetConsoleTailAsync(string instanceName, int lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches live player presence for every instance: whether the supervisor can observe each
    /// one's players, and who it currently sees connected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every instance appears, including the ones with nobody on them.</b> That is the point: a
    /// bare session list makes an absent instance ambiguous between "nobody is online" and "this
    /// game cannot report players", and a consumer rendering the first reading of the second states
    /// something the host does not know. Check
    /// <see cref="WatchdogInstancePresence.IsDetected"/> before reading an empty list as zero.
    /// </para>
    /// <para>
    /// The map reflects who is <em>currently connected</em> — not a historical roster — and it is
    /// volatile: a watchdog restart clears it and it rebuilds as the game logs the next events.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// Presence keyed by instance name, or <c>null</c> when the daemon is unreachable. <b>Null is
    /// not an empty host</b> — it is the supervisor being unavailable, and a caller must report it
    /// as unknown rather than as nobody online.
    /// </returns>
    Task<IReadOnlyDictionary<string, WatchdogInstancePresence>?> GetPlayerPresenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the UPnP port-forward mappings the local IGD currently holds for
    /// <paramref name="instanceName"/> (the rows the watchdog owns, tagged with the instance name).
    /// </summary>
    /// <remarks>
    /// Returns <c>null</c> only when the <b>daemon</b> is unreachable or does not expose the route
    /// (graceful, like <see cref="GetReadyAsync"/> — it does not throw). A reachable daemon whose
    /// <b>router</b> could not be queried
    /// (no IGD, upnpc missing, or a timeout) returns a non-null result with
    /// <see cref="WatchdogUpnpList.State"/> = <c>"unavailable"</c> — distinct from a real query that
    /// found no mappings (<c>"queried"</c> with an empty list). An unavailable state is never presented
    /// as "no forwards".
    /// </remarks>
    /// <param name="instanceName">The instance whose UPnP mappings to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<WatchdogUpnpList?> GetUpnpAsync(string instanceName, CancellationToken cancellationToken = default);
}
