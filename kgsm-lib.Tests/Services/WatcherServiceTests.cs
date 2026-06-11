using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the WatcherService class — thin wrappers over the <c>watcher</c>
/// KGSM subcommands. Pins each method's argument vector and its null guard.
/// </summary>
public class WatcherServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<WatcherService>> _mockLogger;
    private readonly WatcherService _watcherService;

    private const string Instance = "my-server";

    public WatcherServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<WatcherService>>();
        _watcherService = new WatcherService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WatcherService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WatcherService(_mockCommandExecutor.Object, null!));
    }

    private KgsmResult Invoke(string key) => key switch
    {
        "StartWatch" => _watcherService.StartWatch(Instance),
        "TestLogWatch" => _watcherService.TestLogWatch(Instance),
        "TestPortWatch" => _watcherService.TestPortWatch(Instance),
        "GetStatus" => _watcherService.GetStatus(Instance),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "unknown method key"),
    };

    [Theory]
    [InlineData("StartWatch", "watcher start my-server")]
    [InlineData("TestLogWatch", "watcher logs test my-server")]
    [InlineData("TestPortWatch", "watcher ports test my-server")]
    [InlineData("GetStatus", "watcher status my-server")]
    public void WatcherOperation_IssuesExpectedCommand(string key, string expectedArgs)
    {
        string[] expected = expectedArgs.Split(' ');
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => a.SequenceEqual(expected))))
            .Returns(new KgsmResult(new ProcessResult(0, "ok", string.Empty)));

        KgsmResult result = Invoke(key);

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(
            x => x.Execute(It.Is<string[]>(a => a.SequenceEqual(expected))), Times.Once);
    }

    [Theory]
    [InlineData("StartWatch")]
    [InlineData("TestLogWatch")]
    [InlineData("TestPortWatch")]
    [InlineData("GetStatus")]
    public void WatcherOperation_NullInstanceName_ThrowsArgumentNullException(string key)
    {
        Func<KgsmResult> act = key switch
        {
            "StartWatch" => () => _watcherService.StartWatch(null!),
            "TestLogWatch" => () => _watcherService.TestLogWatch(null!),
            "TestPortWatch" => () => _watcherService.TestPortWatch(null!),
            _ => () => _watcherService.GetStatus(null!),
        };

        Assert.Throws<ArgumentNullException>(() => act());
    }
}
