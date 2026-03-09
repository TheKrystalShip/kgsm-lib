using Microsoft.Extensions.Logging;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the ProcessRunner class.
/// </summary>
public class ProcessRunnerTests
{
    private readonly Mock<ILogger<ProcessRunner>> _mockLogger;
    private readonly ProcessRunner _processRunner;

    public ProcessRunnerTests()
    {
        _mockLogger = new Mock<ILogger<ProcessRunner>>();
        _processRunner = new ProcessRunner(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ProcessRunner(null!));
    }

    [Fact]
    public void Execute_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _processRunner.Execute(null!));
    }

    [Fact]
    public void Execute_ValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "test" };

        // Act
        var result = _processRunner.Execute(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("test", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public void Execute_CommandWithMultipleArgs_ExecutesCorrectly()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "hello", "world" };

        // Act
        var result = _processRunner.Execute(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Stdout);
        Assert.Contains("world", result.Stdout);
    }

    [Fact]
    public void Execute_CommandWithNoArgs_ExecutesCorrectly()
    {
        // Arrange
        string command = "pwd";

        // Act
        var result = _processRunner.Execute(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Stdout);
    }

    [Fact]
    public void Execute_NonExistentCommand_ReturnsErrorResult()
    {
        // Arrange
        string command = "this_command_does_not_exist_12345";

        // Act
        var result = _processRunner.Execute(command);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Stderr);
    }

    [Fact]
    public void Execute_LongRunningCommand_WaitsForCompletion()
    {
        // Arrange
        string command = "sleep";
        string[] args = new[] { "0.1" };
        var startTime = DateTime.UtcNow;

        // Act
        var result = _processRunner.Execute(command, args);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.True(duration.TotalMilliseconds >= 100, "Command should have taken at least 100ms");
    }

    [Fact]
    public void Execute_CommandWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "test@#$%^&*()" };

        // Act
        var result = _processRunner.Execute(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("test", result.Stdout);
    }

    [Fact]
    public void Execute_EmptyArgs_ExecutesCommandWithoutArguments()
    {
        // Arrange
        string command = "pwd";
        string[] args = Array.Empty<string>();

        // Act
        var result = _processRunner.Execute(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Stdout);
    }

    [Fact]
    public void Execute_LogsDebugInformation()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "test" };

        // Act
        _processRunner.Execute(command, args);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Executing command")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Execute_TrimsStdoutWhitespace()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "  test  " };

        // Act
        var result = _processRunner.Execute(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("\n", result.Stdout);
        Assert.True(result.Stdout == result.Stdout.Trim());
    }

    #region Async Tests

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "test" };

        // Act
        var result = await _processRunner.ExecuteAsync(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("test", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithMultipleArgs_ExecutesCorrectly()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "hello", "world" };

        // Act
        var result = await _processRunner.ExecuteAsync(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Stdout);
        Assert.Contains("world", result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithNoArgs_ExecutesCorrectly()
    {
        // Arrange
        string command = "pwd";
        string[] args = Array.Empty<string>();

        // Act
        var result = await _processRunner.ExecuteAsync(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentCommand_ReturnsErrorResult()
    {
        // Arrange
        string command = "this_command_does_not_exist_12345";
        string[] args = Array.Empty<string>();

        // Act
        var result = await _processRunner.ExecuteAsync(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_LongRunningCommand_WaitsForCompletion()
    {
        // Arrange
        string command = "sleep";
        string[] args = new[] { "0.1" };
        var startTime = DateTime.UtcNow;

        // Act
        var result = await _processRunner.ExecuteAsync(command, args);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.True(duration.TotalMilliseconds >= 100, "Command should have taken at least 100ms");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        string command = "sleep";
        string[] args = new[] { "10" };
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        var result = await _processRunner.ExecuteAsync(command, args, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("cancelled", result.Stderr.ToLower());
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _processRunner.ExecuteAsync(null!, Array.Empty<string>())
        );
    }

    [Fact]
    public async Task ExecuteAsync_TrimsStdoutWhitespace()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "  test  " };

        // Act
        var result = await _processRunner.ExecuteAsync(command, args);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("\n", result.Stdout);
        Assert.True(result.Stdout == result.Stdout.Trim());
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentExecution_HandlesMultipleProcesses()
    {
        // Arrange
        string command = "echo";
        var tasks = new List<Task<ProcessResult>>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var args = new[] { $"test{i}" };
            tasks.Add(_processRunner.ExecuteAsync(command, args));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.NotNull(results[i]);
            Assert.Equal(0, results[i].ExitCode);
            Assert.Contains($"test{i}", results[i].Stdout);
        }
    }

    #endregion
}

