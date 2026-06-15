using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the LifecycleService class — the layer that actually issues the
/// <c>lifecycle</c> KGSM subcommands. InstanceService forwards most operational
/// calls (start/stop/restart/status/is-active/logs) here, so the behavioral
/// contract for those lives in this fixture; InstanceServiceTests only assert
/// the forwarding.
/// </summary>
public class LifecycleServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<LifecycleService>> _mockLogger;
    private readonly LifecycleService _lifecycleService;

    private const string Instance = "my-server";

    public LifecycleServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<LifecycleService>>();
        _lifecycleService = new LifecycleService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    private static bool ArgsAre(string[] actual, params string[] expected)
        => actual.SequenceEqual(expected);

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(_mockCommandExecutor.Object, null!));
    }

    // --- start / stop / restart / status : Execute("lifecycle", <verb>, name) ---

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("restart")]
    [InlineData("status")]
    public void LifecycleVerb_NullInstanceName_ThrowsArgumentNullException(string verb)
    {
        Func<KgsmResult> act = verb switch
        {
            "start" => () => _lifecycleService.Start(null!),
            "stop" => () => _lifecycleService.Stop(null!),
            "restart" => () => _lifecycleService.Restart(null!),
            _ => () => _lifecycleService.GetStatus(null!),
        };

        Assert.Throws<ArgumentNullException>(() => act());
    }

    [Fact]
    public void Start_ValidInstance_IssuesLifecycleStart()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "start", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "started", string.Empty)));

        KgsmResult result = _lifecycleService.Start(Instance);

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(
            x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "start", Instance))), Times.Once);
    }

    [Fact]
    public void Stop_ValidInstance_IssuesLifecycleStop()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "stop", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "stopped", string.Empty)));

        KgsmResult result = _lifecycleService.Stop(Instance);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Restart_ValidInstance_IssuesLifecycleRestart()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "restart", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "restarted", string.Empty)));

        KgsmResult result = _lifecycleService.Restart(Instance);

        Assert.True(result.IsSuccess);
    }

    // --- provenance: actor/origin propagate as KGSM_EVENT_* env vars on the command ---

    [Fact]
    public void Start_WithActorAndOrigin_PropagatesBothAsEnvironment()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.Is<IReadOnlyDictionary<string, string>>(e =>
                    e.Count == 2
                    && e.ContainsKey("KGSM_EVENT_ACTOR") && e["KGSM_EVENT_ACTOR"] == "discord:haru"
                    && e.ContainsKey("KGSM_EVENT_ORIGIN") && e["KGSM_EVENT_ORIGIN"] == "ui"),
                It.Is<string[]>(a => ArgsAre(a, "lifecycle", "start", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "started", string.Empty)));

        KgsmResult result = _lifecycleService.Start(Instance, actor: "discord:haru", origin: "ui");

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e =>
                e["KGSM_EVENT_ACTOR"] == "discord:haru" && e["KGSM_EVENT_ORIGIN"] == "ui"),
            It.Is<string[]>(a => ArgsAre(a, "lifecycle", "start", Instance))), Times.Once);
    }

    [Fact]
    public void Stop_WithActorOnly_OmitsOriginFromEnvironment()
    {
        // Only the actor is supplied — origin is omitted from the environment entirely
        // (KGSM then emits no origin) rather than carrying an empty/fabricated value.
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.Is<IReadOnlyDictionary<string, string>>(e =>
                    e.Count == 1
                    && e.ContainsKey("KGSM_EVENT_ACTOR") && e["KGSM_EVENT_ACTOR"] == "system:watchdog"
                    && !e.ContainsKey("KGSM_EVENT_ORIGIN")),
                It.Is<string[]>(a => ArgsAre(a, "lifecycle", "stop", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "stopped", string.Empty)));

        KgsmResult result = _lifecycleService.Stop(Instance, actor: "system:watchdog");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Restart_WithOriginOnly_OmitsActorFromEnvironment()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.Is<IReadOnlyDictionary<string, string>>(e =>
                    e.Count == 1
                    && e.ContainsKey("KGSM_EVENT_ORIGIN") && e["KGSM_EVENT_ORIGIN"] == "assistant"
                    && !e.ContainsKey("KGSM_EVENT_ACTOR")),
                It.Is<string[]>(a => ArgsAre(a, "lifecycle", "restart", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "restarted", string.Empty)));

        KgsmResult result = _lifecycleService.Restart(Instance, origin: "assistant");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Start_NoProvenance_UsesPlainCommandPathNotEnvironment()
    {
        // Neither actor nor origin supplied: the no-env command path is used so KGSM
        // applies its own honest fallbacks — the environment overload is never called.
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "start", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "started", string.Empty)));

        KgsmResult result = _lifecycleService.Start(Instance);

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(
            x => x.Execute(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void GetStatus_ExecutionFails_ReturnsFailureResultWithoutThrowing()
    {
        // KgsmResult-returning methods encode failure in the result (IsSuccess=false);
        // they do not throw on a non-zero exit.
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "status", Instance))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "not found")));

        KgsmResult result = _lifecycleService.GetStatus(Instance);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- is-active : Probe (non-zero exit is a normal signal, never an error) ---

    [Fact]
    public void IsActive_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _lifecycleService.IsActive(null!));
    }

    [Fact]
    public void IsActive_ProbeSucceeds_ReturnsTrue()
    {
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "is-active", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "active", string.Empty)));

        Assert.True(_lifecycleService.IsActive(Instance));
    }

    [Fact]
    public void IsActive_ProbeReturnsNonZero_ReturnsFalseWithoutThrowing()
    {
        // A non-zero exit from is-active means "inactive", not an error — Probe
        // surfaces it as IsSuccess=false and IsActive maps that to false.
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "is-active", Instance))))
            .Returns(new KgsmResult(new ProcessResult(3, string.Empty, string.Empty)));

        Assert.False(_lifecycleService.IsActive(Instance));
    }

    // --- logs : Execute("lifecycle","logs",name,"--tail",lines) ---

    [Fact]
    public void GetLogs_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _lifecycleService.GetLogs(null!));
    }

    [Fact]
    public void GetLogs_ValidInstance_ReturnsNonEmptyLineCollection()
    {
        string output = string.Join(Environment.NewLine, "Log line 1", "Log line 2", "Log line 3");
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "logs", Instance, "--tail", "10"))))
            .Returns(new KgsmResult(new ProcessResult(0, output, string.Empty)));

        ICollection<string> logs = _lifecycleService.GetLogs(Instance);

        Assert.Equal(3, logs.Count);
        Assert.Contains("Log line 2", logs);
    }

    [Fact]
    public void GetLogs_CustomLineCount_PassesTailArgument()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "logs", Instance, "--tail", "5"))))
            .Returns(new KgsmResult(new ProcessResult(0, "only-line", string.Empty)));

        ICollection<string> logs = _lifecycleService.GetLogs(Instance, lines: 5);

        Assert.Single(logs);
        _mockCommandExecutor.Verify(
            x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "logs", Instance, "--tail", "5"))), Times.Once);
    }

    [Fact]
    public void GetLogs_ExecutionFails_ThrowsInvalidOperationException()
    {
        // GetLogs returns a bare collection — it has no result channel to carry
        // failure, so a failed command throws (per the interface contract).
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "lifecycle", "logs", Instance, "--tail", "10"))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "log file missing")));

        var ex = Assert.Throws<InvalidOperationException>(() => _lifecycleService.GetLogs(Instance));
        Assert.Contains(Instance, ex.Message);
    }

    [Fact]
    public async Task GetLogsAsync_NullInstanceName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _lifecycleService.GetLogsAsync(null!));
    }

    [Fact]
    public async Task GetLogsAsync_ValidInstance_ReturnsNonEmptyLineCollection()
    {
        string output = string.Join(Environment.NewLine, "a", "b");
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(
                It.Is<string[]>(a => ArgsAre(a, "lifecycle", "logs", Instance, "--tail", "10")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KgsmResult(new ProcessResult(0, output, string.Empty)));

        ICollection<string> logs = await _lifecycleService.GetLogsAsync(Instance);

        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task GetLogsAsync_ExecutionFails_ThrowsInvalidOperationException()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(
                It.Is<string[]>(a => ArgsAre(a, "lifecycle", "logs", Instance, "--tail", "10")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KgsmResult(new ProcessResult(1, string.Empty, "boom")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _lifecycleService.GetLogsAsync(Instance));
        Assert.Contains(Instance, ex.Message);
    }
}
