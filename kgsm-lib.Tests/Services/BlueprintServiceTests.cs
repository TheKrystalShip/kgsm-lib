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
                Ports = [new PortMapping { Start = 2456, End = 2458, Protocol = "udp" }],
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
        Assert.Equal([new PortMapping { Start = 2456, End = 2458, Protocol = "udp" }], result["valheim"].Ports);
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

    [Fact]
    public void ListDetailed_EmptyResult_ReturnsEmptyDictionary()
    {
        // Executor returns a non-null but empty map (KGSM reports no blueprints).
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Blueprint>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "blueprints", "list", "detailed", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Blueprint>?>()))
            .Returns(new Dictionary<string, Blueprint>());

        Dictionary<string, Blueprint> result = _blueprintService.ListDetailed();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ListDetailed_MultipleBlueprints_ReturnsAllBlueprints()
    {
        var expected = new Dictionary<string, Blueprint>
        {
            ["valheim"] = new Blueprint { Name = "valheim", Ports = [new PortMapping { Start = 2456, End = 2458, Protocol = "udp" }] },
            ["factorio"] = new Blueprint { Name = "factorio", Ports = [new PortMapping { Start = 34197, End = 34197, Protocol = "tcp" }] },
            ["7dtd"] = new Blueprint { Name = "7dtd", Ports = [new PortMapping { Start = 26900, End = 26900, Protocol = "tcp" }] },
        };
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Blueprint>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "blueprints", "list", "detailed", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Blueprint>?>()))
            .Returns(expected);

        Dictionary<string, Blueprint> result = _blueprintService.ListDetailed();

        Assert.Equal(3, result.Count);
        Assert.Equal([new PortMapping { Start = 34197, End = 34197, Protocol = "tcp" }], result["factorio"].Ports);
        Assert.True(result.ContainsKey("valheim"));
        Assert.True(result.ContainsKey("7dtd"));
    }

    [Fact]
    public void ListDetailed_LogsDebugInformation()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Blueprint>>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Blueprint>?>()))
            .Returns(new Dictionary<string, Blueprint> { ["valheim"] = new Blueprint { Name = "valheim" } });

        _blueprintService.ListDetailed();

        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // --- FindAll : every candidate path, existence only ---

    [Fact]
    public void FindAll_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _blueprintService.FindAll(null!));
    }

    [Fact]
    public void FindAll_OverriddenBlueprint_ReportsBothCandidatesExisting()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<BlueprintCandidates>(
                It.Is<string[]>(a => a.SequenceEqual(new[] { "blueprints", "find", "palworld", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<BlueprintCandidates?>()))
            .Returns(new BlueprintCandidates
            {
                Name = "palworld",
                Resolved = "/home/x/.local/share/kgsm/blueprints/palworld.bp.yaml",
                Candidates =
                [
                    new BlueprintCandidate { Tier = BlueprintTier.User, Path = "/home/x/.local/share/kgsm/blueprints/palworld.bp.yaml", Exists = true },
                    new BlueprintCandidate { Tier = BlueprintTier.System, Path = "/opt/kgsm/blueprints/palworld.bp.yaml", Exists = true },
                ],
            });

        BlueprintCandidates? result = _blueprintService.FindAll("palworld");

        Assert.NotNull(result);
        Assert.True(result.OverridesSystem);
        Assert.True(result.HasSystemOriginal);
        Assert.Equal(BlueprintTier.User, result.User!.Tier);
    }

    [Fact]
    public void FindAll_UserOnlyBlueprint_ReportsNoSystemOriginal()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<BlueprintCandidates>(
                It.IsAny<string[]>(), It.IsAny<Action<JsonSerializerOptions>?>(), It.IsAny<BlueprintCandidates?>()))
            .Returns(new BlueprintCandidates
            {
                Name = "teamfortress2",
                Resolved = "/home/x/.local/share/kgsm/blueprints/teamfortress2.bp.yaml",
                Candidates =
                [
                    new BlueprintCandidate { Tier = BlueprintTier.User, Path = "/home/x/.local/share/kgsm/blueprints/teamfortress2.bp.yaml", Exists = true },
                    new BlueprintCandidate { Tier = BlueprintTier.System, Path = "/opt/kgsm/blueprints/teamfortress2.bp.yaml", Exists = false },
                ],
            });

        BlueprintCandidates? result = _blueprintService.FindAll("teamfortress2");

        Assert.NotNull(result);
        Assert.False(result.HasSystemOriginal); // nothing to revert to
        Assert.False(result.OverridesSystem);
    }

    [Fact]
    public void FindAll_NameResolvesToNothing_ReturnsNull()
    {
        // The engine exits non-zero with no JSON, which the executor surfaces as the default.
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<BlueprintCandidates>(
                It.IsAny<string[]>(), It.IsAny<Action<JsonSerializerOptions>?>(), It.IsAny<BlueprintCandidates?>()))
            .Returns((BlueprintCandidates?)null);

        Assert.Null(_blueprintService.FindAll("ghost"));
    }

    // --- Validate : an invalid verdict is a successful check, not a failed command ---

    [Fact]
    public void Validate_NullNameOrPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _blueprintService.Validate(null!));
    }

    [Fact]
    public void Validate_ValidBlueprint_ReturnsPassingVerdict()
    {
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(a => a.SequenceEqual(new[] { "blueprints", "validate", "factorio", "--json" }))))
            .Returns(new KgsmResult(0, """{"Valid": true, "Path": "/opt/kgsm/blueprints/factorio.bp.yaml", "Errors": []}"""));

        BlueprintValidation? result = _blueprintService.Validate("factorio");

        Assert.NotNull(result);
        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidBlueprint_ReturnsTheErrorListDespiteTheNonZeroExit()
    {
        // The engine reports an invalid blueprint as EC_INVALID_BLUEPRINT (28) with the verdict on
        // stdout. Probing rather than executing is what keeps that verdict from being discarded.
        _mockCommandExecutor
            .Setup(x => x.Probe(It.IsAny<string[]>()))
            .Returns(new KgsmResult(28, """{"Valid": false, "Path": "/tmp/draft.bp.yaml", "Errors": ["missing runtime", "missing name"]}"""));

        BlueprintValidation? result = _blueprintService.Validate("/tmp/draft.bp.yaml");

        Assert.NotNull(result);
        Assert.False(result.Valid);
        Assert.Equal(["missing runtime", "missing name"], result.Errors);
    }

    [Fact]
    public void Validate_NoVerdictAtAll_ReturnsNullRatherThanAssumingAPass()
    {
        // A name that resolves to nothing exits non-zero with only stderr — unknown, never a pass.
        _mockCommandExecutor
            .Setup(x => x.Probe(It.IsAny<string[]>()))
            .Returns(new KgsmResult(27, "", "Blueprint not found: ghost"));

        Assert.Null(_blueprintService.Validate("ghost"));
    }

    [Fact]
    public void Validate_UnparseableVerdict_ReturnsNull()
    {
        _mockCommandExecutor
            .Setup(x => x.Probe(It.IsAny<string[]>()))
            .Returns(new KgsmResult(0, "not json at all"));

        Assert.Null(_blueprintService.Validate("factorio"));
    }
}
