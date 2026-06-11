using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the KgsmClient class.
/// </summary>
public class KgsmClientTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<IBlueprintService> _mockBlueprintService;
    private readonly Mock<IInstanceService> _mockInstanceService;
    private readonly Mock<IEventService> _mockEventService;
    private readonly Mock<IConfigService> _mockConfigService;
    private readonly Mock<ILifecycleService> _mockLifecycleService;
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<IDirectoryService> _mockDirectoryService;
    private readonly Mock<IWatcherService> _mockWatcherService;
    private readonly Mock<INetworkService> _mockNetworkService;
    private readonly Mock<ISystemService> _mockSystemService;
    private readonly Mock<IEventManagementService> _mockEventManagementService;
    private readonly Mock<ILogger<KgsmClient>> _mockLogger;
    private readonly KgsmClient _kgsmClient;

    public KgsmClientTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockBlueprintService = new Mock<IBlueprintService>();
        _mockInstanceService = new Mock<IInstanceService>();
        _mockEventService = new Mock<IEventService>();
        _mockConfigService = new Mock<IConfigService>();
        _mockLifecycleService = new Mock<ILifecycleService>();
        _mockFileService = new Mock<IFileService>();
        _mockDirectoryService = new Mock<IDirectoryService>();
        _mockWatcherService = new Mock<IWatcherService>();
        _mockNetworkService = new Mock<INetworkService>();
        _mockSystemService = new Mock<ISystemService>();
        _mockEventManagementService = new Mock<IEventManagementService>();
        _mockLogger = new Mock<ILogger<KgsmClient>>();

        _kgsmClient = new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            null!,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullBlueprintService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            null!,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullInstanceService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            null!,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullEventService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            null!,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullConfigService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            null!,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullLifecycleService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            null!,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullFileService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            null!,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullDirectoryService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            null!,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullWatcherService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            null!,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullNetworkService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            null!,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            _mockEventManagementService.Object,
            null!
        ));
    }

    [Fact]
    public void Constructor_NullSystemService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            null!,
            _mockEventManagementService.Object,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_NullEventManagementService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KgsmClient(
            _mockCommandExecutor.Object,
            _mockBlueprintService.Object,
            _mockInstanceService.Object,
            _mockEventService.Object,
            _mockConfigService.Object,
            _mockLifecycleService.Object,
            _mockFileService.Object,
            _mockDirectoryService.Object,
            _mockWatcherService.Object,
            _mockNetworkService.Object,
            _mockSystemService.Object,
            null!,
            _mockLogger.Object
        ));
    }

    [Fact]
    public void Constructor_ValidParameters_InitializesEventService()
    {
        // Assert
        _mockEventService.Verify(x => x.Initialize(), Times.Once);
    }

    [Fact]
    public void Blueprints_Property_ReturnsInjectedService()
    {
        // Assert
        Assert.NotNull(_kgsmClient.Blueprints);
        Assert.Same(_mockBlueprintService.Object, _kgsmClient.Blueprints);
    }

    [Fact]
    public void Instances_Property_ReturnsInjectedService()
    {
        // Assert
        Assert.NotNull(_kgsmClient.Instances);
        Assert.Same(_mockInstanceService.Object, _kgsmClient.Instances);
    }

    [Fact]
    public void Events_Property_ReturnsInjectedService()
    {
        // Assert
        Assert.NotNull(_kgsmClient.Events);
        Assert.Same(_mockEventService.Object, _kgsmClient.Events);
    }

    [Fact]
    public void Network_Property_ReturnsInjectedService()
    {
        // Assert
        Assert.NotNull(_kgsmClient.Network);
        Assert.Same(_mockNetworkService.Object, _kgsmClient.Network);
    }

    [Fact]
    public void System_Property_ReturnsInjectedService()
    {
        // Assert
        Assert.NotNull(_kgsmClient.System);
        Assert.Same(_mockSystemService.Object, _kgsmClient.System);
    }

    [Fact]
    public void EventManagement_Property_ReturnsInjectedService()
    {
        // Assert
        Assert.NotNull(_kgsmClient.EventManagement);
        Assert.Same(_mockEventManagementService.Object, _kgsmClient.EventManagement);
    }

    [Fact]
    public void Help_ExecutesHelpCommand_ReturnsResult()
    {
        // Arrange
        var expectedOutput = "KGSM Help Information";
        var expectedResult = new KgsmResult(new ProcessResult(0, expectedOutput, string.Empty));
        _mockCommandExecutor
            .Setup(x => x.Execute("help"))
            .Returns(expectedResult);

        // Act
        var result = _kgsmClient.Help();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectedOutput, result.Stdout);
        // KgsmClient.Help() invokes the bare "help" subcommand (see KgsmClient.cs).
        _mockCommandExecutor.Verify(x => x.Execute("help"), Times.Once);
    }

    [Fact]
    public void HelpInteractive_ExecutesInteractiveHelpCommand_ReturnsResult()
    {
        // Arrange
        var expectedOutput = "KGSM Interactive Help";
        var expectedResult = new KgsmResult(new ProcessResult(0, expectedOutput, string.Empty));
        _mockCommandExecutor
            .Setup(x => x.Execute("interactive", "help"))
            .Returns(expectedResult);

        // Act
        var result = _kgsmClient.HelpInteractive();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectedOutput, result.Stdout);
        _mockCommandExecutor.Verify(x => x.Execute("interactive", "help"), Times.Once);
    }

    [Fact]
    public void GetVersion_ExecutesVersionCommand_ReturnsResult()
    {
        // Arrange
        var expectedVersion = "1.0.0";
        var expectedResult = new KgsmResult(new ProcessResult(0, expectedVersion, string.Empty));
        _mockCommandExecutor
            .Setup(x => x.Execute("--version"))
            .Returns(expectedResult);

        // Act
        var result = _kgsmClient.GetVersion();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectedVersion, result.Stdout);
        _mockCommandExecutor.Verify(x => x.Execute("--version"), Times.Once);
    }

    [Fact]
    public void AdHoc_ValidCommand_ExecutesSuccessfully()
    {
        // Arrange
        string[] args = new[] { "--custom", "command", "--option" };
        var expectedResult = new KgsmResult(new ProcessResult(0, "Success", string.Empty));
        _mockCommandExecutor
            .Setup(x => x.Execute(args))
            .Returns(expectedResult);

        // Act
        var result = _kgsmClient.AdHoc(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        _mockCommandExecutor.Verify(x => x.Execute(args), Times.Once);
    }

    [Fact]
    public void AdHoc_FailedCommand_ReturnsErrorResult()
    {
        // Arrange
        string[] args = new[] { "--invalid", "command" };
        var expectedResult = new KgsmResult(new ProcessResult(1, string.Empty, "Command failed"));
        _mockCommandExecutor
            .Setup(x => x.Execute(args))
            .Returns(expectedResult);

        // Act
        var result = _kgsmClient.AdHoc(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.ExitCode);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void AdHoc_EmptyArgs_ExecutesWithEmptyArguments()
    {
        // Arrange
        string[] args = Array.Empty<string>();
        var expectedResult = new KgsmResult(new ProcessResult(0, "Success", string.Empty));
        _mockCommandExecutor
            .Setup(x => x.Execute(args))
            .Returns(expectedResult);

        // Act
        var result = _kgsmClient.AdHoc(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Help_LogsDebugInformation()
    {
        // Arrange
        var expectedResult = new KgsmResult(new ProcessResult(0, "Help", string.Empty));
        _mockCommandExecutor
            .Setup(x => x.Execute("help"))
            .Returns(expectedResult);

        // Act
        _kgsmClient.Help();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("help")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
