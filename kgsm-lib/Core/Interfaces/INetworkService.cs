using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for querying and managing network configuration for KGSM instances.
/// Maps the <c>kgsm network</c> CLI module.
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Checks whether a port is currently in use.
    /// </summary>
    /// <param name="port">The port number to check (1–65535).</param>
    /// <param name="protocol">The protocol to check: <c>"tcp"</c> or <c>"udp"</c>. Defaults to <c>"tcp"</c>.</param>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing port status text.
    /// <see cref="KgsmResult.IsSuccess"/> is <c>true</c> when the port is free, <c>false</c> when in use.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is outside 1–65535.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="protocol"/> is not <c>"tcp"</c> or <c>"udp"</c>.</exception>
    KgsmResult CheckPort(int port, string protocol = "tcp");

    /// <summary>
    /// Lists all ports currently in use on the system.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> whose <see cref="KgsmResult.Stdout"/> contains a formatted table of in-use ports.
    /// </returns>
    KgsmResult ListUsedPorts();

    /// <summary>
    /// Checks KGSM-managed instances for port conflicts.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> whose <see cref="KgsmResult.Stdout"/> describes any detected conflicts.
    /// <see cref="KgsmResult.IsSuccess"/> is <c>true</c> when no conflicts are found.
    /// </returns>
    KgsmResult FindConflicts();

    /// <summary>
    /// Kills the process that is using the specified port. Requires elevated privileges (sudo).
    /// </summary>
    /// <param name="port">The port number whose owning process should be terminated (1–65535).</param>
    /// <param name="protocol">The protocol to target: <c>"tcp"</c> or <c>"udp"</c>. Defaults to <c>"tcp"</c>.</param>
    /// <returns>
    /// A <see cref="KgsmResult"/> indicating whether the process was successfully killed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is outside 1–65535.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="protocol"/> is not <c>"tcp"</c> or <c>"udp"</c>.</exception>
    KgsmResult KillPort(int port, string protocol = "tcp");

    /// <summary>
    /// Tests whether a specific port is accessible from outside the host.
    /// </summary>
    /// <param name="port">The port number to test (1–65535).</param>
    /// <param name="protocol">The protocol to test: <c>"tcp"</c> or <c>"udp"</c>. Defaults to <c>"tcp"</c>.</param>
    /// <returns>
    /// A <see cref="KgsmResult"/> whose <see cref="KgsmResult.Stdout"/> contains diagnostic information about port accessibility.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is outside 1–65535.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="protocol"/> is not <c>"tcp"</c> or <c>"udp"</c>.</exception>
    KgsmResult TestPort(int port, string protocol = "tcp");

    /// <summary>
    /// Tests the accessibility of all ports used by KGSM-managed instances.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> whose <see cref="KgsmResult.Stdout"/> contains a formatted table of results for all instance ports.
    /// </returns>
    KgsmResult TestAllPorts();

    /// <summary>
    /// Retrieves the server's external and local IP addresses.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> whose <see cref="KgsmResult.Stdout"/> contains the detected IP address information.
    /// </returns>
    KgsmResult GetIp();

    /// <summary>
    /// Retrieves the list of DNS servers configured on the host.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> whose <see cref="KgsmResult.Stdout"/> contains the DNS server list.
    /// </returns>
    KgsmResult GetDns();
}
