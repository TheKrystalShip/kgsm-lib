using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheKrystalShip.KGSM.Extensions;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies the typed kgsm-watchdog control client. The socket transport itself is
/// integration territory (like <see cref="UnixSocketClient"/>, deliberately not in
/// the unit suite), so these tests lock the two things that break silently without
/// a live daemon: (1) the camelCase wire contract — every DTO must bind through the
/// source-generated <see cref="KgsmJsonContext"/> exactly as the daemon serializes
/// it, since a casing mismatch deserializes to all-defaults with no error; and
/// (2) construction/DI invariants. The JSON literals below match the daemon's
/// compact camelCase output (kgsm-watchdog: Model/Contracts.cs + WatchdogJsonContext).
/// </summary>
public class WatchdogClientTests
{
    // --- Wire-contract (casing) guards ---

    [Fact]
    public void InstanceState_DeserializesCamelCaseWireShape()
    {
        const string json =
            """{"name":"7dtd","desired":"running","populated":true,"pid":12345,"cgroupPath":"/sys/fs/cgroup/kgsm.slice/7dtd","phase":"running","restarts":0,"reason":"started"}""";

        var state = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogInstanceState);

        Assert.NotNull(state);
        Assert.Equal("7dtd", state!.Name);
        Assert.Equal("running", state.Desired);
        Assert.True(state.Populated);
        Assert.Equal(12345, state.Pid);
        Assert.Equal("/sys/fs/cgroup/kgsm.slice/7dtd", state.CgroupPath);
        Assert.Equal("running", state.Phase);
        Assert.Equal(0, state.Restarts);
        Assert.Equal("started", state.Reason);
    }

    [Fact]
    public void InstanceState_NullPid_BindsNull_NotFabricated()
    {
        // A stopped/failed instance reports pid:null — the daemon never fabricates one.
        const string json =
            """{"name":"7dtd","desired":"stopped","populated":false,"pid":null,"cgroupPath":"/sys/fs/cgroup/kgsm.slice/7dtd","phase":"failed","restarts":3,"reason":"restart limit reached (3 consecutive failures, last exit 137); gave up after 2 retries"}""";

        var state = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogInstanceState);

        Assert.NotNull(state);
        Assert.False(state!.Populated);
        Assert.Null(state.Pid);
        Assert.Equal("failed", state.Phase);
        Assert.Equal(3, state.Restarts);
        Assert.Contains("gave up after 2 retries", state.Reason);
    }

    [Fact]
    public void ActionResult_DeserializesCamelCase()
    {
        const string json = """{"instance":"7dtd","ok":true,"message":"started"}""";

        var result = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogActionResult);

        Assert.NotNull(result);
        Assert.Equal("7dtd", result!.Instance);
        Assert.True(result.Ok);
        Assert.Equal("started", result.Message);
    }

    [Fact]
    public void ActionResult_Conflict_BindsOkFalse()
    {
        // The 409 (already-in-desired-state) body the client returns rather than throwing.
        const string json = """{"instance":"7dtd","ok":false,"message":"already running"}""";

        var result = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogActionResult);

        Assert.NotNull(result);
        Assert.False(result!.Ok);
        Assert.Equal("already running", result.Message);
    }

    [Fact]
    public void ReadyState_DeserializesCamelCase()
    {
        const string json = """{"ready":true,"detail":"root-bootstrapped; dropped to uid 1000"}""";

        var ready = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogReadyState);

        Assert.NotNull(ready);
        Assert.True(ready!.Ready);
        Assert.Equal("root-bootstrapped; dropped to uid 1000", ready.Detail);
    }

    [Fact]
    public void InstanceStateArray_DeserializesListShape()
    {
        const string json =
            """[{"name":"7dtd","desired":"running","populated":true,"pid":1,"cgroupPath":"/c/7dtd","phase":"running","restarts":0,"reason":"ok"},{"name":"factorio","desired":"stopped","populated":false,"pid":null,"cgroupPath":"/c/factorio","phase":"stopped","restarts":0,"reason":"stopped"}]""";

        var states = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogInstanceStateArray);

        Assert.NotNull(states);
        Assert.Equal(2, states!.Length);
        Assert.Equal("7dtd", states[0].Name);
        Assert.True(states[0].Populated);
        Assert.Equal("factorio", states[1].Name);
        Assert.Null(states[1].Pid);
    }

    // --- Construction / DI invariants ---

    [Fact]
    public void Ctor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(
            // Cast disambiguates from the internal test-only WatchdogClient(HttpClient, …) ctor.
            () => new WatchdogClient((WatchdogClientOptions)null!, NullLogger<WatchdogClient>.Instance));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_BlankSocketPath_Throws(string socketPath)
        => Assert.Throws<ArgumentException>(
            () => new WatchdogClient(new WatchdogClientOptions { SocketPath = socketPath }, NullLogger<WatchdogClient>.Instance));

    [Fact]
    public void Options_DefaultSocketPath_MatchesDaemonDefault()
        => Assert.Equal("/run/kgsm-watchdog/control.sock", new WatchdogClientOptions().SocketPath);

    [Fact]
    public void AddKgsmWatchdogClient_RegistersResolvableClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<WatchdogClient>>(NullLogger<WatchdogClient>.Instance);
        services.AddKgsmWatchdogClient("/run/kgsm-watchdog/control.sock");

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<IWatchdogClient>();

        Assert.NotNull(client);
        Assert.IsType<WatchdogClient>(client);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddKgsmWatchdogClient_BlankSocketPath_Throws(string socketPath)
        => Assert.Throws<ArgumentException>(
            () => new ServiceCollection().AddKgsmWatchdogClient(socketPath));
}
