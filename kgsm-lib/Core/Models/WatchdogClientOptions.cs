namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Options for the kgsm-watchdog control client. The watchdog is a separate
/// resident daemon from the kgsm event socket, so its connection is configured
/// independently of <see cref="KgsmOptions"/>.
/// </summary>
public class WatchdogClientOptions
{
    /// <summary>
    /// Path to the watchdog control unix-domain socket. Defaults to the daemon's
    /// own default (<c>KGSM_WATCHDOG_SOCKET</c>). The socket's filesystem perms are
    /// the security boundary — the client performs no in-band authentication.
    /// </summary>
    public string SocketPath { get; set; } = "/run/kgsm-watchdog/control.sock";

    /// <summary>
    /// Per-request timeout. The default covers a graceful stop's bounded drain on
    /// a slow-stopping server; lower it for latency-sensitive callers.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(150);
}
