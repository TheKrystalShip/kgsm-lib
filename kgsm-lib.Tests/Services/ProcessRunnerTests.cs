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
    public void Execute_SingleArgWithSpaces_PassedAsOneArgv()
    {
        // Regression: an argument containing spaces must reach the process as ONE argv,
        // not be re-split on whitespace. `printf '%s' "alpha beta gamma"` echoes the single
        // operand verbatim → "alpha beta gamma"; if the arg were split into 3 operands the
        // recycled %s would print "alphabetagamma". (The old space-joined Arguments string
        // mangled every multi-word console command / config value.)
        var result = _processRunner.Execute("printf", "%s", "alpha beta gamma");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("alpha beta gamma", result.Stdout);
    }

    [Fact]
    public void Execute_WithEnvironment_SingleArgWithSpaces_PreservedAndEnvApplied()
    {
        // The provenance overload (env + args) is the exact path the console-input command
        // takes; the spaced arg must survive AND the env var must be layered on.
        var env = new Dictionary<string, string> { ["KGSM_TEST_VAR"] = "x" };
        var result = _processRunner.Execute("printf", TimeSpan.FromSeconds(10), env, new[] { "%s", "say hello world" });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("say hello world", result.Stdout);
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

    #region Timeout Tests

    [Fact]
    public void Execute_WithExplicitTimeout_KillsProcessThatExceedsTimeout()
    {
        // Arrange: a command that sleeps far longer than the tiny timeout we give it.
        string command = "sleep";
        string[] args = new[] { "5" };
        var startTime = DateTime.UtcNow;

        // Act
        var result = _processRunner.Execute(command, TimeSpan.FromMilliseconds(200), args);
        var duration = DateTime.UtcNow - startTime;

        // Assert: failure returned promptly (not after the process's own 5s) with a timeout message.
        Assert.NotNull(result);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("timed out", result.Stderr.ToLower());
        Assert.True(duration.TotalSeconds < 4, "Process should have been killed well before its own 5s sleep");
    }

    [Fact]
    public void Execute_WithExplicitTimeout_AllowsProcessThatCompletesInTime()
    {
        // Arrange
        string command = "echo";
        string[] args = new[] { "fast" };

        // Act: generous timeout, quick command — should succeed normally.
        var result = _processRunner.Execute(command, TimeSpan.FromSeconds(10), args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("fast", result.Stdout);
    }

    [Fact]
    public void Execute_DefaultTimeoutOverload_DelegatesAndSucceeds()
    {
        // The parameterless-timeout overload must still behave as before.
        var result = _processRunner.Execute("echo", "hello");

        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Stdout);
    }

    #endregion

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
    public async Task ExecuteAsync_SingleArgWithSpaces_PassedAsOneArgv()
    {
        // Async counterpart of the argv regression — a spaced arg stays one operand.
        var result = await _processRunner.ExecuteAsync("printf", new[] { "%s", "alpha beta gamma" });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("alpha beta gamma", result.Stdout);
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

