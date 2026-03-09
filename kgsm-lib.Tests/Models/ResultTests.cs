using System.Text.Json;

namespace TheKrystalShip.KGSM.Tests.Models;

/// <summary>
/// Tests for the ProcessResult model class.
/// </summary>
public class ProcessResultTests
{
    [Fact]
    public void ProcessResult_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        int exitCode = 0;
        string stdout = "Output message";
        string stderr = "Error message";

        // Act
        var result = new ProcessResult(exitCode, stdout, stderr);

        // Assert
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(stdout, result.Stdout);
        Assert.Equal(stderr, result.Stderr);
    }

    [Fact]
    public void ProcessResult_DefaultValues_HandlesEmptyStrings()
    {
        // Act
        var result = new ProcessResult(0, string.Empty, string.Empty);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public void ProcessResult_NonZeroExitCode_StoresCorrectly()
    {
        // Act
        var result = new ProcessResult(1, "output", "error");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.NotEmpty(result.Stderr);
    }
}

/// <summary>
/// Tests for the KgsmResult model class.
/// </summary>
public class KgsmResultTests
{
    [Fact]
    public void KgsmResult_ConstructorWithExitCodeAndMessage_SetsPropertiesCorrectly()
    {
        // Arrange
        int exitCode = 0;
        string message = "Operation successful";

        // Act
        var result = new KgsmResult(exitCode, message);

        // Assert
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(message, result.Stdout);
    }

    [Fact]
    public void KgsmResult_ConstructorWithProcessResult_CopiesProperties()
    {
        // Arrange
        var processResult = new ProcessResult(0, "stdout message", "stderr message");

        // Act
        var kgsmResult = new KgsmResult(processResult);

        // Assert
        Assert.Equal(processResult.ExitCode, kgsmResult.ExitCode);
        Assert.Equal(processResult.Stdout, kgsmResult.Stdout);
    }

    [Fact]
    public void KgsmResult_ConstructorWithProcessResult_HandlesStderr()
    {
        // Arrange
        var processResult = new ProcessResult(1, string.Empty, "error message");

        // Act
        var kgsmResult = new KgsmResult(processResult);

        // Assert
        Assert.Equal(1, kgsmResult.ExitCode);
    }
}
