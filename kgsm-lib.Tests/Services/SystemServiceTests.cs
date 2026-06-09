using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the SystemService class.
/// </summary>
public class SystemServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<SystemService>> _mockLogger;
    private readonly SystemService _systemService;

    public SystemServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<SystemService>>();
        _systemService = new SystemService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SystemService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SystemService(_mockCommandExecutor.Object, null!));
    }

    // --- Shutdown ---

    [Fact]
    public void Shutdown_NegativeDelay_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _systemService.Shutdown(-1));
    }

    [Fact]
    public void Shutdown_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "shutdown", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Shutdown scheduled", string.Empty)));

        KgsmResult result = _systemService.Shutdown();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Shutdown_WithDelay_PassesDelayArgument()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "shutdown", "5" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Shutdown scheduled in 5 minutes", string.Empty)));

        KgsmResult result = _systemService.Shutdown(5);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Shutdown_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "shutdown", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Permission denied")));

        KgsmResult result = _systemService.Shutdown();

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- Restart ---

    [Fact]
    public void Restart_NegativeDelay_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _systemService.Restart(-1));
    }

    [Fact]
    public void Restart_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "restart", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Restart scheduled", string.Empty)));

        KgsmResult result = _systemService.Restart();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Restart_WithDelay_PassesDelayArgument()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "restart", "10" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Restart scheduled in 10 minutes", string.Empty)));

        KgsmResult result = _systemService.Restart(10);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Restart_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "restart", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Permission denied")));

        KgsmResult result = _systemService.Restart();

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- CancelScheduled ---

    [Fact]
    public void CancelScheduled_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "cancel" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Scheduled operation cancelled", string.Empty)));

        KgsmResult result = _systemService.CancelScheduled();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void CancelScheduled_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "cancel" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Nothing to cancel")));

        KgsmResult result = _systemService.CancelScheduled();

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- GetUptime ---

    [Fact]
    public void GetUptime_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "uptime" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "up 3 days, 4 hours, 12 minutes", string.Empty)));

        KgsmResult result = _systemService.GetUptime();

        Assert.True(result.IsSuccess);
        Assert.Equal("up 3 days, 4 hours, 12 minutes", result.Stdout);
    }

    [Fact]
    public void GetUptime_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "uptime" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Command failed")));

        KgsmResult result = _systemService.GetUptime();

        Assert.False(result.IsSuccess);
    }

    // --- GetLoad ---

    [Fact]
    public void GetLoad_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "load" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "0.12 0.34 0.56", string.Empty)));

        KgsmResult result = _systemService.GetLoad();

        Assert.True(result.IsSuccess);
        Assert.Equal("0.12 0.34 0.56", result.Stdout);
    }

    [Fact]
    public void GetLoad_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "load" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Command failed")));

        KgsmResult result = _systemService.GetLoad();

        Assert.False(result.IsSuccess);
    }

    // --- GetMemory ---

    [Fact]
    public void GetMemory_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "memory" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Total: 16G Used: 8G Free: 8G", string.Empty)));

        KgsmResult result = _systemService.GetMemory();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GetMemory_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "memory" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Command failed")));

        KgsmResult result = _systemService.GetMemory();

        Assert.False(result.IsSuccess);
    }

    // --- GetDisk ---

    [Fact]
    public void GetDisk_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "disk" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Total: 500G Used: 200G Free: 300G", string.Empty)));

        KgsmResult result = _systemService.GetDisk();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GetDisk_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "disk" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Command failed")));

        KgsmResult result = _systemService.GetDisk();

        Assert.False(result.IsSuccess);
    }

    // --- IsRebootRequired ---

    [Fact]
    public void IsRebootRequired_RebootNeeded_ReturnsTrue()
    {
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "reboot-required" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "yes", string.Empty)));

        bool result = _systemService.IsRebootRequired();

        Assert.True(result);
    }

    [Fact]
    public void IsRebootRequired_NoRebootNeeded_ReturnsFalse()
    {
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "reboot-required" }))))
            .Returns(new KgsmResult(new ProcessResult(1, "no", string.Empty)));

        bool result = _systemService.IsRebootRequired();

        Assert.False(result);
    }

    // --- GetInfo (text) ---

    [Fact]
    public void GetInfo_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "info" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "System info text", string.Empty)));

        KgsmResult result = _systemService.GetInfo();

        Assert.True(result.IsSuccess);
        Assert.Equal("System info text", result.Stdout);
    }

    [Fact]
    public void GetInfo_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "system", "info" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Command failed")));

        KgsmResult result = _systemService.GetInfo();

        Assert.False(result.IsSuccess);
    }

    // --- GetInfo<T> (JSON) ---

    [Fact]
    public void GetInfoGeneric_SuccessfulExecution_ReturnsDeserializedObject()
    {
        var expected = new Dictionary<string, string>
        {
            ["uptime"] = "3 days",
            ["reboot_required"] = "no"
        };

        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, string>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "system", "info", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, string>?>()))
            .Returns(expected);

        Dictionary<string, string>? result = _systemService.GetInfo<Dictionary<string, string>>();

        Assert.NotNull(result);
        Assert.Equal("3 days", result["uptime"]);
        Assert.Equal("no", result["reboot_required"]);
    }

    [Fact]
    public void GetInfoGeneric_ExecutionFails_ReturnsNull()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, string>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "system", "info", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, string>?>()))
            .Returns((Dictionary<string, string>?)null);

        Dictionary<string, string>? result = _systemService.GetInfo<Dictionary<string, string>>();

        Assert.Null(result);
    }
}
