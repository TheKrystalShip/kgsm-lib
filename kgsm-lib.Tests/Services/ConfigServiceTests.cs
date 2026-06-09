using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the ConfigService class.
/// </summary>
public class ConfigServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<ConfigService>> _mockLogger;
    private readonly ConfigService _configService;

    public ConfigServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<ConfigService>>();
        _configService = new ConfigService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConfigService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConfigService(_mockCommandExecutor.Object, null!));
    }

    [Fact]
    public void Get_NullKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _configService.Get(null!));
    }

    [Fact]
    public void Get_WhitespaceKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _configService.Get("   "));
    }

    [Fact]
    public void Get_SuccessfulExecution_ReturnsValue()
    {
        // Arrange
        const string key = "enable_logging";
        const string expectedValue = "true";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "get", key }))))
            .Returns(new KgsmResult(new ProcessResult(0, expectedValue, string.Empty)));

        // Act
        string? result = _configService.Get(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void Get_ExecutionFails_ReturnsNull()
    {
        // Arrange
        const string key = "invalid_key";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "get", key }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Key not found")));

        // Act
        string? result = _configService.Get(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Get_EmptyStdout_ReturnsNull()
    {
        // Arrange
        const string key = "empty_key";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "get", key }))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        // Act
        string? result = _configService.Get(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_NullKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _configService.Set(null!, "value"));
    }

    [Fact]
    public void Set_WhitespaceKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _configService.Set("   ", "value"));
    }

    [Fact]
    public void Set_NullValue_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _configService.Set("key", null!));
    }

    [Fact]
    public void Set_WhitespaceValue_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _configService.Set("key", "   "));
    }

    [Fact]
    public void Set_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string key = "enable_logging";
        const string value = "true";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "set", $"{key}={value}" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Configuration updated successfully", string.Empty)));

        // Act
        KgsmResult result = _configService.Set(key, value);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Set_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const string key = "invalid_key";
        const string value = "invalid_value";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "set", $"{key}={value}" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Validation failed")));

        // Act
        KgsmResult result = _configService.Set(key, value);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void List_SuccessfulExecution_ReturnsDictionary()
    {
        // Arrange
        var expectedConfig = new Dictionary<string, string>
        {
            ["enable_logging"] = "true",
            ["enable_systemd"] = "false",
            ["instance_suffix_length"] = "3"
        };

        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, string>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "config", "list", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, string>?>()))
            .Returns(expectedConfig);

        // Act
        Dictionary<string, string> result = _configService.List();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("true", result["enable_logging"]);
        Assert.Equal("false", result["enable_systemd"]);
        Assert.Equal("3", result["instance_suffix_length"]);
    }

    [Fact]
    public void List_ExecutionFails_ReturnsEmptyDictionary()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, string>>(
                It.Is<string[]>(args => args.SequenceEqual(new[] { "config", "list", "--json" })),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, string>?>()))
            .Returns((Dictionary<string, string>?)null);

        // Act
        Dictionary<string, string> result = _configService.List();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Reset_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "reset" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Configuration reset to defaults", string.Empty)));

        // Act
        KgsmResult result = _configService.Reset();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Reset_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "reset" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to reset configuration")));

        // Act
        KgsmResult result = _configService.Reset();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Validate_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "validate" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Configuration is valid", string.Empty)));

        // Act
        KgsmResult result = _configService.Validate();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Validate_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "validate" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Validation failed: invalid value for enable_logging")));

        // Act
        KgsmResult result = _configService.Validate();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Merge_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "merge" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Merged successfully", string.Empty)));

        // Act
        KgsmResult result = _configService.Merge();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Merge_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "merge" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to merge configuration")));

        // Act
        KgsmResult result = _configService.Merge();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Rollback_DefaultGeneration_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "rollback", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Rolled back to generation 0", string.Empty)));

        // Act
        KgsmResult result = _configService.Rollback();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Rollback_SpecificGeneration_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "rollback", "3" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Rolled back to generation 3", string.Empty)));

        // Act
        KgsmResult result = _configService.Rollback(3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Rollback_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "rollback", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to rollback configuration")));

        // Act
        KgsmResult result = _configService.Rollback();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Rollback_InvalidGeneration_ThrowsArgumentOutOfRangeException(int generation)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _configService.Rollback(generation));
    }

    [Fact]
    public void Diff_DefaultGeneration_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "diff", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "--- backup\n+++ current\n@@ -1 +1 @@", string.Empty)));

        // Act
        KgsmResult result = _configService.Diff();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Diff_SpecificGeneration_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "diff", "5" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "--- backup.5\n+++ current\n@@ -1 +1 @@", string.Empty)));

        // Act
        KgsmResult result = _configService.Diff(5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Diff_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "config", "diff", "0" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to retrieve diff")));

        // Act
        KgsmResult result = _configService.Diff();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Diff_InvalidGeneration_ThrowsArgumentOutOfRangeException(int generation)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _configService.Diff(generation));
    }
}
