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
}
