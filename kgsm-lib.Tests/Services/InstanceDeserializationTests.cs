using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Pins the <see cref="Instance"/> wire shape from <c>instances info &lt;name&gt; --json</c>, focused
/// on <see cref="Instance.Runtime"/>. With systemd removed, <c>runtime</c> (native|container) is the
/// SOLE supervision discriminator (kgsm-watchdog gates on it), so its binding is load-bearing —
/// and the property carries no <c>[JsonPropertyName]</c>, relying on case-insensitive matching of
/// KGSM's lowercase <c>runtime</c> field. These guard that, plus the safety net that a missing field
/// falls to <c>Native</c> and the removed legacy fields are tolerated rather than thrown on.
/// </summary>
public class InstanceDeserializationTests
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

    private Instance? Info() =>
        Create().ExecuteForJson<Instance>(["instances", "info", "7dtd", "--json"]);

    [Theory]
    [InlineData("native", InstanceRuntime.Native)]
    [InlineData("container", InstanceRuntime.Container)]
    [InlineData("NATIVE", InstanceRuntime.Native)]       // KGSM emits lowercase; binding is case-insensitive
    [InlineData("Container", InstanceRuntime.Container)]
    public void Runtime_binds_case_insensitively_from_the_wire_field(string wire, InstanceRuntime expected)
    {
        StubProcessOutput($$"""{"name":"7dtd","runtime":"{{wire}}"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal(expected, result!.Runtime);
    }

    [Fact]
    public void Runtime_defaults_to_Native_when_the_field_is_absent()
    {
        StubProcessOutput("""{"name":"7dtd"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal(InstanceRuntime.Native, result!.Runtime);
    }

    [Fact]
    public void CgroupPath_binds_from_the_wire_field()
    {
        // KGSM emits the derived native cgroup directory; the monitor reads it to sample
        // cgroup counters directly. Binds via [JsonPropertyName("cgroup_path")].
        StubProcessOutput("""{"name":"7dtd","runtime":"native","cgroup_path":"/sys/fs/cgroup/kgsm.slice/7dtd"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal("/sys/fs/cgroup/kgsm.slice/7dtd", result!.CgroupPath);
    }

    [Fact]
    public void CgroupPath_defaults_to_empty_when_absent()
    {
        // Older KGSM (and container instances) emit no cgroup_path — it must deserialize to
        // empty, the signal the monitor uses to fall back to its /proc-tree probe.
        StubProcessOutput("""{"name":"7dtd","runtime":"native"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.CgroupPath);
    }

    [Fact]
    public void PlayerPresenceRegexes_bind_from_the_wire_fields()
    {
        // KGSM materializes the blueprint's player_joined_regex / player_left_regex into the
        // instance config, which the generic instances-info JSON dump emits. The watchdog reads
        // these off the native Instance to tail its log. Bind via [JsonPropertyName("player_*_regex")].
        StubProcessOutput(
            """{"name":"factorio-01","runtime":"native","player_joined_regex":"\\[JOIN\\] (?<name>.+) joined","player_left_regex":"\\[LEAVE\\] (?<name>.+) left"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal("\\[JOIN\\] (?<name>.+) joined", result!.PlayerJoinedRegex);
        Assert.Equal("\\[LEAVE\\] (?<name>.+) left", result.PlayerLeftRegex);
    }

    [Fact]
    public void PlayerPresenceRegexes_default_to_empty_when_absent()
    {
        // No blueprint pattern set (or a pre-1.20.0 KGSM) → empty, the watchdog's signal that
        // native detection is disabled for this instance (honest unknown, no event invented).
        StubProcessOutput("""{"name":"7dtd","runtime":"native"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.PlayerJoinedRegex);
        Assert.Equal(string.Empty, result.PlayerLeftRegex);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    public void EnablePortForwarding_binds_from_the_stringly_bool_wire(string wire, bool expected)
    {
        // KGSM's instances-info dump renders every config value as a STRING (jq -R), so the UPnP gate
        // arrives as "true"/"false" — NOT a JSON bool. The global JsonStringToBoolConverter coerces it
        // (case-insensitive), the same path enable_firewall_management already rides. The watchdog reads
        // this off the spec (GetInstanceInfo) to gate upnpc — if it didn't bind, UPnP could never be
        // enabled. Binds via [JsonPropertyName("enable_port_forwarding")].
        StubProcessOutput(
            "{\"name\":\"factorio-01\",\"runtime\":\"native\",\"enable_port_forwarding\":\"" + wire + "\"}");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal(expected, result!.EnablePortForwarding);
    }

    [Fact]
    public void EnablePortForwarding_defaults_to_false_when_absent()
    {
        // An instance whose config predates the gate (or a pre-restore KGSM) emits no key → false =
        // inert (the safe default; the watchdog runs no upnpc until the operator enables it).
        StubProcessOutput("""{"name":"7dtd","runtime":"native"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.False(result!.EnablePortForwarding);
    }

    [Fact]
    public void Legacy_systemd_and_lifecycle_manager_fields_are_ignored_not_thrown_on()
    {
        // Older KGSM still emits lifecycle_manager / enable_systemd / systemd_* — their properties were
        // removed, so they must deserialize as unmapped (skipped), not throw and collapse the read.
        StubProcessOutput("""
            {"name":"7dtd","runtime":"native","lifecycle_manager":"systemd","enable_systemd":"true","systemd_service_file":"/etc/systemd/system/7dtd.service","systemd_socket_file":"/etc/systemd/system/7dtd.socket"}
            """);

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal("7dtd", result!.Name);
        Assert.Equal(InstanceRuntime.Native, result.Runtime);
    }

    [Fact]
    public void Ports_bind_from_the_structured_array_real_factorio_shape()
    {
        // Captured verbatim from `kgsm instances info factorio-test --json | jq -c .ports`:
        // a proto-less single port expands to one tcp + one udp mapping, each start==end.
        StubProcessOutput("""
            {"name":"7dtd","runtime":"native","ports":[{"start":34197,"end":34197,"protocol":"tcp"},{"start":34197,"end":34197,"protocol":"udp"}]}
            """);

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.Equal(
            [new PortMapping { Start = 34197, End = 34197, Protocol = "tcp" },
             new PortMapping { Start = 34197, End = 34197, Protocol = "udp" }],
            result!.Ports);
    }

    [Fact]
    public void Ports_preserve_ranges_as_a_single_mapping()
    {
        // A UFW range stays ONE {start,end} mapping (range-preserving) — not unrolled on the wire.
        StubProcessOutput("""
            {"name":"7dtd","runtime":"native","ports":[{"start":27015,"end":27020,"protocol":"udp"}]}
            """);

        Instance? result = Info();

        Assert.NotNull(result);
        PortMapping only = Assert.Single(result!.Ports);
        Assert.Equal(27015, only.Start);
        Assert.Equal(27020, only.End);
        Assert.Equal("udp", only.Protocol);
    }

    [Fact]
    public void Ports_default_to_empty_list_when_absent()
    {
        // Containers (Docker owns ports) and older KGSM emit no `ports` — must be an empty list,
        // never null, so consumers can enumerate without a null check.
        StubProcessOutput("""{"name":"7dtd","runtime":"native"}""");

        Instance? result = Info();

        Assert.NotNull(result);
        Assert.NotNull(result!.Ports);
        Assert.Empty(result.Ports);
    }

    [Fact]
    public void Ports_start_end_also_bind_from_stringly_numbers()
    {
        // Defensive: even if start/end arrive as KGSM's stringly scalars, the global
        // string->int coercion binds them — either wire shape works.
        StubProcessOutput("""
            {"name":"7dtd","runtime":"native","ports":[{"start":"80","end":"80","protocol":"tcp"}]}
            """);

        Instance? result = Info();

        Assert.NotNull(result);
        PortMapping only = Assert.Single(result!.Ports);
        Assert.Equal(80, only.Start);
        Assert.Equal(80, only.End);
    }
}
