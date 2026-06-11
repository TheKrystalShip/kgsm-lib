using System.Reflection;
using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the FileService class. Every method is a thin wrapper that builds a
/// <c>files [component] &lt;verb&gt; &lt;instance&gt;</c> command and forwards it to the
/// executor, so the contract worth pinning is the exact argument vector each
/// method produces (a wrong component/verb silently performs the wrong operation).
/// </summary>
public class FileServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<FileService>> _mockLogger;
    private readonly FileService _fileService;

    private const string Instance = "my-server";

    public FileServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<FileService>>();
        _fileService = new FileService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FileService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FileService(_mockCommandExecutor.Object, null!));
    }

    // key → the exact "files ..." argument vector that key's method must issue.
    private KgsmResult Invoke(string key) => key switch
    {
        "Create" => _fileService.Create(Instance),
        "CreateManage" => _fileService.CreateManage(Instance),
        "CreateSystemd" => _fileService.CreateSystemd(Instance),
        "CreateUfw" => _fileService.CreateUfw(Instance),
        "CreateSymlink" => _fileService.CreateSymlink(Instance),
        "CreateUpnp" => _fileService.CreateUpnp(Instance),
        "Remove" => _fileService.Remove(Instance),
        "RemoveSystemd" => _fileService.RemoveSystemd(Instance),
        "RemoveUfw" => _fileService.RemoveUfw(Instance),
        "RemoveSymlink" => _fileService.RemoveSymlink(Instance),
        "RemoveUpnp" => _fileService.RemoveUpnp(Instance),
        "RemoveManage" => _fileService.RemoveManage(Instance),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "unknown method key"),
    };

    [Theory]
    [InlineData("Create", "files create my-server")]
    [InlineData("CreateManage", "files management create my-server")]
    [InlineData("CreateSystemd", "files systemd enable my-server")]
    [InlineData("CreateUfw", "files ufw enable my-server")]
    [InlineData("CreateSymlink", "files symlink enable my-server")]
    [InlineData("CreateUpnp", "files upnp enable my-server")]
    [InlineData("Remove", "files remove my-server")]
    [InlineData("RemoveSystemd", "files systemd disable my-server")]
    [InlineData("RemoveUfw", "files ufw disable my-server")]
    [InlineData("RemoveSymlink", "files symlink disable my-server")]
    [InlineData("RemoveUpnp", "files upnp disable my-server")]
    [InlineData("RemoveManage", "files management remove my-server")]
    public void FileOperation_IssuesExpectedCommand(string key, string expectedArgs)
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

    // Validation lives in the shared ExecuteFileOperation helper, so one representative
    // method covers it: ArgumentException.ThrowIfNullOrWhiteSpace → ArgumentNullException
    // for null, ArgumentException for whitespace.
    [Fact]
    public void FileOperation_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _fileService.Create(null!));
    }

    [Fact]
    public void FileOperation_WhitespaceInstanceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _fileService.Create("   "));
    }

    // CreateConfig/RemoveConfig issue "files config install|uninstall", but kgsm removed
    // the standalone 'files config' component (commit 75644d7 deleted files.config.sh) —
    // config-file handling folded into files create/remove. Those methods now hit kgsm's
    // "Unknown component" path and always fail, so they are deprecated rather than pinned
    // as a "correct" command. This guards the deprecation against accidental removal and
    // documents the drift until they're dropped at the next major version.
    [Theory]
    [InlineData("CreateConfig")]
    [InlineData("RemoveConfig")]
    public void RemovedConfigComponentMethods_AreMarkedObsolete(string methodName)
    {
        MethodInfo method = typeof(FileService).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<ObsoleteAttribute>());
    }
}
