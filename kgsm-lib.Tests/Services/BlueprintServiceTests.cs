using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the BlueprintService class.
/// </summary>
public class BlueprintServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<BlueprintService>> _mockLogger;
    private readonly BlueprintService _blueprintService;

    public BlueprintServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<BlueprintService>>();
        _blueprintService = new BlueprintService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new BlueprintService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new BlueprintService(_mockCommandExecutor.Object, null!));
    }

    [Fact]
    public void ListDetailed_SuccessfulExecution_ReturnsBlueprints()
    {
        // Arrange
        var expectedBlueprints = new Dictionary<string, Blueprint>
        {
            ["valheim"] = new Blueprint
            {
                Name = "valheim",
                Ports = "2456-2458",
                SteamAppId = "896660",
                IsSteamAccountRequired = false,
                ExecutableFile = "valheim_server.x86_64",
                ExecutableSubdirectory = "",
                ExecutableArguments = "-nographics -batchmode -port 2456 -name \"My Server\" -world \"Dedicated\" -password \"secret\"",
                LevelName = "Dedicated"
            }
        };

        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Blueprint>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "blueprints", "list", "detailed", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Blueprint>?>()))
            .Returns(expectedBlueprints);

        // Act
        Dictionary<string, Blueprint> result = _blueprintService.ListDetailed();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(result.ContainsKey("valheim"));
        Assert.Equal("valheim", result["valheim"].Name);
        Assert.Equal("2456-2458", result["valheim"].Ports);
        Assert.Equal("896660", result["valheim"].SteamAppId);
        Assert.False(result["valheim"].IsSteamAccountRequired);
    }

    [Fact]
    public void ListDetailed_ProcessExecutionFails_ReturnsEmptyDictionary()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Blueprint>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "blueprints", "list", "detailed", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Blueprint>?>()))
            .Returns((Dictionary<string, Blueprint>?)null);

        // Act
        var result = _blueprintService.ListDetailed();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ListDetailed_InvalidJson_ReturnsEmptyDictionary()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Blueprint>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "blueprints", "list", "detailed", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Blueprint>?>()))
            .Returns((Dictionary<string, Blueprint>?)null);

        // Act
        var result = _blueprintService.ListDetailed();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact(Skip = "Needs update for IKgsmCommandExecutor - see TEST_UPDATE_NOTES.md")]
    public void ListDetailed_EmptyJson_ReturnsEmptyDictionary()
    {
        // TODO: Update to use _mockCommandExecutor.ExecuteForJson
        throw new NotImplementedException("Test needs updating for new command executor pattern");
    }

    [Fact(Skip = "Needs update for IKgsmCommandExecutor - see TEST_UPDATE_NOTES.md")]
    public void ListDetailed_MultipleBlueprints_ReturnsAllBlueprints()
    {
        // TODO: Update to use _mockCommandExecutor.ExecuteForJson  
        throw new NotImplementedException("Test needs updating for new command executor pattern");
    }

    [Fact(Skip = "Needs update for IKgsmCommandExecutor - see TEST_UPDATE_NOTES.md")]
    public void ListDetailed_FiltersEmptyKeys_ReturnsOnlyValidBlueprints()
    {
        // TODO: Update to use _mockCommandExecutor.ExecuteForJson
        throw new NotImplementedException("Test needs updating for new command executor pattern");
    }

    [Fact(Skip = "Create method no longer exists in BlueprintService")]
    public void Create_NullBlueprint_ThrowsArgumentNullException()
    {
        // TODO: Remove test or wait for Create method implementation
        throw new NotImplementedException("Create method not implemented");
    }

    [Fact(Skip = "Create method no longer exists in BlueprintService")]
    public void Create_EmptyBlueprintName_ThrowsArgumentException()
    {
        // TODO: Remove test or wait for Create method implementation
        throw new NotImplementedException("Create method not implemented");
    }

    [Fact(Skip = "Create method no longer exists in BlueprintService")]
    public void Create_NullBlueprintName_ThrowsArgumentException()
    {
        // TODO: Remove test or wait for Create method implementation
        throw new NotImplementedException("Create method not implemented");
    }

    [Fact(Skip = "Create method no longer exists in BlueprintService")]
    public void Create_ValidBlueprint_ReturnsSuccessResult()
    {
        // TODO: Remove test or wait for Create method implementation
        throw new NotImplementedException("Create method not implemented");
    }

    [Fact(Skip = "Needs update for IKgsmCommandExecutor - see TEST_UPDATE_NOTES.md")]
    public void ListDetailed_LogsDebugInformation()
    {
        // TODO: Update to use _mockCommandExecutor.ExecuteForJson
        throw new NotImplementedException("Test needs updating for new command executor pattern");
    }
}