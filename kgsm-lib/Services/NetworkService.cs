using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the <see cref="INetworkService"/> interface for querying and managing
/// network configuration via the KGSM <c>network</c> CLI module.
/// </summary>
public class NetworkService : INetworkService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<NetworkService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkService"/> class.
    /// </summary>
    /// <param name="commandExecutor">The command executor used to run KGSM commands.</param>
    /// <param name="logger">The logger used for diagnostic output.</param>
    public NetworkService(IKgsmCommandExecutor commandExecutor, ILogger<NetworkService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("NetworkService initialized");
    }

    /// <inheritdoc/>
    public KgsmResult CheckPort(int port, string protocol = "tcp")
    {
        ValidatePort(port);
        ValidateProtocol(protocol);

        // Exit code is a signal here: non-zero simply means the port is in use.
        return _commandExecutor.Probe("network", "ports", "check", port.ToString(), protocol);
    }

    /// <inheritdoc/>
    public KgsmResult ListUsedPorts()
        => _commandExecutor.Execute("network", "ports", "list-used");

    /// <inheritdoc/>
    public List<HostPort>? ListUsedPortsDetailed()
    {
        // Null is carried through rather than collapsed to an empty list: the engine emits the
        // array only on the success path, so null means the read could not be made and an empty
        // array means it was made and found nothing. Those are different answers, and a caller
        // that cannot tell them apart reports a failed scan as an idle host.
        return _commandExecutor.ExecuteForJson<List<HostPort>>(
            ["network", "ports", "list-used", "--json"]);
    }

    /// <inheritdoc/>
    public KgsmResult FindConflicts()
    {
        // Exit code is a signal here: non-zero means conflicts were detected.
        return _commandExecutor.Probe("network", "ports", "conflicts");
    }

    /// <inheritdoc/>
    public List<PortConflict>? FindConflictsDetailed()
    {
        // A clean host emits an empty array, which is the same shape as a host with findings — so
        // nothing here recognises a sentinel word to learn there were none. It matters most on this
        // read: no conflicts is the ordinary answer, so collapsing a failed scan into it would
        // report "all clear" on a host nobody managed to check.
        return _commandExecutor.ExecuteForJson<List<PortConflict>>(
            ["network", "ports", "conflicts", "--json"]);
    }

    /// <inheritdoc/>
    public KgsmResult KillPort(int port, string protocol = "tcp")
    {
        ValidatePort(port);
        ValidateProtocol(protocol);

        return _commandExecutor.Execute("network", "ports", "kill", port.ToString(), protocol);
    }

    /// <inheritdoc/>
    public KgsmResult TestPort(int port, string protocol = "tcp")
    {
        ValidatePort(port);
        ValidateProtocol(protocol);

        return _commandExecutor.Execute("network", "test-port", port.ToString(), protocol);
    }

    /// <inheritdoc/>
    public KgsmResult TestAllPorts()
        => _commandExecutor.Execute("network", "test-all");

    /// <inheritdoc/>
    public KgsmResult GetIp()
        => _commandExecutor.Execute("network", "ip");

    /// <inheritdoc/>
    public KgsmResult GetDns()
        => _commandExecutor.Execute("network", "dns");

    private static void ValidatePort(int port)
    {
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");
    }

    private static void ValidateProtocol(string protocol)
    {
        if (!protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) &&
            !protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Protocol must be \"tcp\" or \"udp\".", nameof(protocol));
        }
    }
}
