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
    /// Probes supervisor readiness. Returns <c>true</c> only when the daemon
    /// answers <c>GET /ready</c> with 200 (in-slice and able to spawn). A daemon
    /// that is down or whose socket is stale yields <c>false</c> rather than
    /// throwing, so this doubles as a presence check.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the supervisor readiness detail (the <c>/ready</c> body, served on
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
}
