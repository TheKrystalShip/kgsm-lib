namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies the real KGSM wire shapes for <c>network ports list-used --json</c> and
/// <c>network ports conflicts --json</c> deserialize into <see cref="HostPort"/> and
/// <see cref="PortConflict"/>.
///
/// The JSON below is captured verbatim from a live host. It guards the two things the plain-text
/// forms of these commands could not express: a listening port whose owning process could not be
/// attributed carries a <see langword="null"/> process rather than a placeholder name, and a host
/// with no conflicts emits the same empty array a failed parse would — which is why nothing above
/// this layer reads a message to learn whether anything was found.
/// </summary>
public class HostPortDeserializationTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<ILogger<KgsmCommandExecutor>> _logger = new();

    private KgsmCommandExecutor Create() =>
        new(_processRunner.Object,
            new KgsmOptions { KgsmPath = "/opt/kgsm/kgsm.sh", Timeouts = new KgsmTimeoutOptions() },
            _logger.Object);

    private void StubProcessOutput(string stdout) =>
        _processRunner
            .Setup(r => r.Execute(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Returns(new ProcessResult(0, stdout, string.Empty));

    // Captured verbatim from `kgsm network ports list-used --json` on a live host.
    private const string LiveUsedPortsJson = """
        [
          {"port": 22, "protocol": "tcp", "process": null},
          {"port": 8082, "protocol": "tcp", "process": "llama-server"},
          {"port": 8211, "protocol": "udp", "process": "PalServer-Linux"}
        ]
        """;

    private const string LiveConflictsJson = """
        [
          {"kind": "instance", "port": 27015, "protocol": "udp", "instance": "Ketchup", "other": "romestead"},
          {"kind": "external", "port": 25565, "protocol": "tcp", "instance": "minecraft", "other": "java:392616"}
        ]
        """;

    [Fact]
    public void ListedPorts_CarryPortProtocolAndProcess()
    {
        StubProcessOutput(LiveUsedPortsJson);

        var ports = Create().ExecuteForJson<List<HostPort>>(
            ["network", "ports", "list-used", "--json"]);

        Assert.NotNull(ports);
        Assert.Equal(3, ports!.Count);

        Assert.Equal(8082, ports[1].Port);
        Assert.Equal("tcp", ports[1].Protocol);
        Assert.Equal("llama-server", ports[1].Process);
    }

    [Fact]
    public void AnUnattributedSocket_ReportsANullProcess()
    {
        StubProcessOutput(LiveUsedPortsJson);

        var ports = Create().ExecuteForJson<List<HostPort>>(
            ["network", "ports", "list-used", "--json"]);

        // The port is still a measurement; only who holds it is unknown.
        Assert.NotNull(ports);
        Assert.Equal(22, ports![0].Port);
        Assert.Null(ports[0].Process);
    }

    [Fact]
    public void Conflicts_DistinguishAnInstancePairFromAnOutsideProcess()
    {
        StubProcessOutput(LiveConflictsJson);

        var conflicts = Create().ExecuteForJson<List<PortConflict>>(
            ["network", "ports", "conflicts", "--json"]);

        Assert.NotNull(conflicts);
        Assert.Equal(2, conflicts!.Count);

        Assert.Equal("instance", conflicts[0].Kind);
        Assert.Equal(27015, conflicts[0].Port);
        Assert.Equal("udp", conflicts[0].Protocol);
        Assert.Equal("Ketchup", conflicts[0].Instance);
        Assert.Equal("romestead", conflicts[0].Other);

        Assert.Equal("external", conflicts[1].Kind);
        Assert.Equal("java:392616", conflicts[1].Other);
    }

    [Fact]
    public void AHostWithNoConflicts_DeserializesToAnEmptyList()
    {
        StubProcessOutput("[]");

        var conflicts = Create().ExecuteForJson<List<PortConflict>>(
            ["network", "ports", "conflicts", "--json"]);

        Assert.NotNull(conflicts);
        Assert.Empty(conflicts!);
    }
}
