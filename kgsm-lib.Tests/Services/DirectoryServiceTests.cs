using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the DirectoryService class.
/// </summary>
public class DirectoryServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<DirectoryService>> _mockLogger;
    private readonly DirectoryService _directoryService;

    public DirectoryServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<DirectoryService>>();
        _directoryService = new DirectoryService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DirectoryService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DirectoryService(_mockCommandExecutor.Object, null!));
    }

    [Fact]
    public void Create_NullInstanceName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _directoryService.Create(null!));
    }

    [Fact]
    public void Create_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string instanceName = "my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "create", instanceName }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Created", string.Empty)));

        // Act
        KgsmResult result = _directoryService.Create(instanceName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Remove_NullInstanceName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _directoryService.Remove(null!));
    }

    [Fact]
    public void Remove_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string instanceName = "my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "remove", instanceName }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Removed", string.Empty)));

        // Act
        KgsmResult result = _directoryService.Remove(instanceName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void LinkInstance_NullBlueprint_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _directoryService.LinkInstance(null!, "my-server", "/opt/servers/my-server"));
    }

    [Fact]
    public void LinkInstance_WhitespaceInstanceName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _directoryService.LinkInstance("valheim", "   ", "/opt/servers/my-server"));
    }

    [Fact]
    public void LinkInstance_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string blueprint = "valheim";
        const string instanceName = "my-server";
        const string workingDir = "/opt/servers/my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "link-instance", blueprint, instanceName, workingDir }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Linked", string.Empty)));

        // Act
        KgsmResult result = _directoryService.LinkInstance(blueprint, instanceName, workingDir);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void LinkInstance_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const string blueprint = "valheim";
        const string instanceName = "my-server";
        const string workingDir = "/opt/servers/my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "link-instance", blueprint, instanceName, workingDir }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to create symlink")));

        // Act
        KgsmResult result = _directoryService.LinkInstance(blueprint, instanceName, workingDir);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void UnlinkInstance_NullBlueprint_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _directoryService.UnlinkInstance(null!, "my-server"));
    }

    [Fact]
    public void UnlinkInstance_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string blueprint = "valheim";
        const string instanceName = "my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "unlink-instance", blueprint, instanceName }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Unlinked", string.Empty)));

        // Act
        KgsmResult result = _directoryService.UnlinkInstance(blueprint, instanceName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void UnlinkInstance_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const string blueprint = "valheim";
        const string instanceName = "my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "unlink-instance", blueprint, instanceName }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to remove symlink")));

        // Act
        KgsmResult result = _directoryService.UnlinkInstance(blueprint, instanceName);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void EnsureCreated_NullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _directoryService.EnsureCreated(null!));
    }

    [Fact]
    public void EnsureCreated_WhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _directoryService.EnsureCreated("   "));
    }

    [Fact]
    public void EnsureCreated_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string path = "/opt/game-servers/my-server";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "ensure-created", path }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Directory created", string.Empty)));

        // Act
        KgsmResult result = _directoryService.EnsureCreated(path);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void EnsureCreated_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const string path = "/root/restricted-path";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "directories", "ensure-created", path }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Permission denied")));

        // Act
        KgsmResult result = _directoryService.EnsureCreated(path);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }
}
