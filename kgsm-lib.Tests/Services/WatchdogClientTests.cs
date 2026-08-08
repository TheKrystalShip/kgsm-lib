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

    // --- UPnP wire-contract (casing) guards ---

    [Fact]
    public void UpnpList_DeserializesCamelCaseWireShape()
    {
        const string json =
            """{"instance":"factorio-test","state":"queried","mappings":[{"externalPort":34197,"protocol":"udp","internalPort":34197,"internalClient":"192.168.1.128","description":"factorio-test"}]}""";

        var list = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogUpnpList);

        Assert.NotNull(list);
        Assert.Equal("factorio-test", list!.Instance);
        Assert.Equal("queried", list.State);
        var m = Assert.Single(list.Mappings);
        Assert.Equal(34197, m.ExternalPort);
        Assert.Equal("udp", m.Protocol);
        Assert.Equal(34197, m.InternalPort);
        Assert.Equal("192.168.1.128", m.InternalClient);
        Assert.Equal("factorio-test", m.Description);
    }

    [Fact]
    public void UpnpList_UnavailableState_BindsEmpty_NotFabricated()
    {
        // A reachable daemon whose router couldn't be queried → "unavailable" with no mappings; this
        // must NOT read as "no forwards".
        const string json = """{"instance":"factorio-test","state":"unavailable","mappings":[]}""";

        var list = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.WatchdogUpnpList);

        Assert.NotNull(list);
        Assert.Equal("unavailable", list!.State);
        Assert.Empty(list.Mappings);
    }

    // --- Request-shape + response-parse over a stub transport (no live daemon) ---

    [Fact]
    public async Task GetUpnpAsync_HitsCorrectRoute_AndParsesState()
    {
        var handler = new CapturingHandler(
            """{"instance":"factorio-test","state":"queried","mappings":[]}""");
        using var client = new WatchdogClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
            NullLogger<WatchdogClient>.Instance);

        var list = await client.GetUpnpAsync("factorio-test");

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("/upnp/factorio-test", handler.LastPath);
        Assert.NotNull(list);
        Assert.Equal("queried", list!.State);
    }

    [Fact]
    public async Task GetUpnpAsync_DaemonUnreachable_ReturnsNull_DoesNotThrow()
    {
        var handler = new CapturingHandler(throws: true);
        using var client = new WatchdogClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
            NullLogger<WatchdogClient>.Instance);

        Assert.Null(await client.GetUpnpAsync("factorio-test"));
    }

    [Fact]
    public async Task ForgetAsync_SendsDeleteToInstanceRoute()
    {
        var handler = new CapturingHandler(
            """{"instance":"factorio-test","ok":true,"message":"deregistered"}""");
        using var client = new WatchdogClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
            NullLogger<WatchdogClient>.Instance);

        var result = await client.ForgetAsync("factorio-test");

        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal("/instance/factorio-test", handler.LastPath);
        Assert.Equal("", handler.LastBody);
        Assert.True(result.Ok);
        Assert.Equal("deregistered", result.Message);
    }

    /// <summary>
    /// A stub <see cref="HttpMessageHandler"/> that captures the request shape and returns a canned JSON
    /// body — so the UPnP request routing/serialization and response parsing are unit-tested without a
    /// live daemon socket (the transport itself is integration territory).
    /// </summary>
    private sealed class CapturingHandler(string responseJson = "{}", bool throws = false) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string LastPath { get; private set; } = "";
        public string LastQuery { get; private set; } = "";
        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (throws)
                throw new HttpRequestException("connection refused");

            LastMethod = request.Method;
            LastPath = request.RequestUri!.AbsolutePath;
            LastQuery = request.RequestUri!.Query;
            LastBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
