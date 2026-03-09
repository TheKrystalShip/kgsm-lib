using Microsoft.Extensions.Logging;
using KgsmLogLevel = TheKrystalShip.KGSM.Core.Models.Enums.LogLevel;

namespace TheKrystalShip.KGSM.Tests.Utilities;

/// <summary>
/// Tests for the LogParser utility class.
/// </summary>
public class LogParserTests
{
    [Fact]
    public void ParseLogLine_NullRawLine_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => LogParser.ParseLogLine(null!, "test-instance"));
    }

    [Fact]
    public void ParseLogLine_NullInstanceName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => LogParser.ParseLogLine("test log", null!));
    }

    [Fact]
    public void ParseLogLine_SimpleLine_ReturnsLogEntry()
    {
        // Arrange
        string rawLine = "This is a simple log message";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(instanceName, result.InstanceName);
        Assert.Equal(rawLine, result.RawLine);
        Assert.Contains("simple log message", result.Message);
    }

    [Theory]
    [InlineData("[2024-01-15 10:30:45.123] [INFO] [Main] Test message", "Test message", KgsmLogLevel.Info)]
    [InlineData("[2024-01-15 10:30:45] [ERROR] Error occurred", "Error occurred", KgsmLogLevel.Error)]
    [InlineData("[2024-01-15 10:30:45] [DEBUG] Debug info", "Debug info", KgsmLogLevel.Debug)]
    [InlineData("[2024-01-15 10:30:45] [WARNING] Warning message", "Warning message", KgsmLogLevel.Warning)]
    public void ParseLogLine_BracketedFormat_ParsesCorrectly(string rawLine, string expectedMessage, KgsmLogLevel expectedLevel)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(expectedLevel, result.Level);
        Assert.Equal(instanceName, result.InstanceName);
    }

    [Theory]
    [InlineData("2024-01-15T10:30:45.123Z INFO Test message", "Test message", KgsmLogLevel.Info)]
    [InlineData("2024-01-15T10:30:45Z ERROR Error occurred", "Error occurred", KgsmLogLevel.Error)]
    [InlineData("2024-01-15T10:30:45.123 DEBUG Debug info", "Debug info", KgsmLogLevel.Debug)]
    public void ParseLogLine_ISO8601Format_ParsesCorrectly(string rawLine, string expectedMessage, KgsmLogLevel expectedLevel)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Theory]
    [InlineData("Jan 15 10:30:45 INFO: Server started", "Server started", KgsmLogLevel.Info)]
    [InlineData("Dec 25 23:59:59 ERROR: Connection failed", "Connection failed", KgsmLogLevel.Error)]
    public void ParseLogLine_SyslogFormat_ParsesCorrectly(string rawLine, string expectedMessage, KgsmLogLevel expectedLevel)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Theory]
    [InlineData("10:30:45.123 [INFO] Test message", "Test message", KgsmLogLevel.Info)]
    [InlineData("23:59:59 [ERROR] Error occurred", "Error occurred", KgsmLogLevel.Error)]
    public void ParseLogLine_TimeOnlyFormat_ParsesCorrectly(string rawLine, string expectedMessage, KgsmLogLevel expectedLevel)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Theory]
    [InlineData("INFO: Server started", "Server started", KgsmLogLevel.Info)]
    [InlineData("ERROR: Connection failed", "Connection failed", KgsmLogLevel.Error)]
    [InlineData("WARNING: Low memory", "Low memory", KgsmLogLevel.Warning)]
    [InlineData("DEBUG: Processing request", "Processing request", KgsmLogLevel.Debug)]
    public void ParseLogLine_SimpleLevelFormat_ParsesCorrectly(string rawLine, string expectedMessage, KgsmLogLevel expectedLevel)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(expectedLevel, result.Level);
    }

    [Fact]
    public void ParseLogLine_WithThreadInformation_ParsesThreadId()
    {
        // Arrange
        string rawLine = "[2024-01-15 10:30:45] [INFO] [Thread-123] Test message";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Thread-123", result.ThreadId);
        Assert.Equal("Test message", result.Message);
    }

    [Theory]
    [InlineData("This is an ERROR message")]
    [InlineData("Server FAILED to start")]
    [InlineData("EXCEPTION occurred")]
    public void ParseLogLine_MessageContainsErrorKeywords_SetsErrorLevel(string rawLine)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.Equal(KgsmLogLevel.Error, result.Level);
    }

    [Theory]
    [InlineData("This is a WARNING")]
    [InlineData("WARN: Low disk space")]
    public void ParseLogLine_MessageContainsWarningKeywords_SetsWarningLevel(string rawLine)
    {
        // Arrange
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.Equal(KgsmLogLevel.Warning, result.Level);
    }

    [Fact]
    public void ParseLogLine_EmptyLine_ReturnsEmptyMessage()
    {
        // Arrange
        string rawLine = "   ";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Message);
    }

    [Theory]
    [InlineData("TRACE")]
    [InlineData("TRC")]
    [InlineData("T")]
    public void ParseLogLine_TraceLevel_ParsesCorrectly(string level)
    {
        // Arrange
        string rawLine = $"{level}: Trace message";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.Equal(KgsmLogLevel.Trace, result.Level);
    }

    [Theory]
    [InlineData("FATAL")]
    [InlineData("CRITICAL")]
    [InlineData("CRIT")]
    public void ParseLogLine_FatalLevel_ParsesCorrectly(string level)
    {
        // Arrange
        string rawLine = $"{level}: Fatal error";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.Equal(KgsmLogLevel.Fatal, result.Level);
    }

    [Fact]
    public void ParseLogContent_MultipleLines_ParsesAllLines()
    {
        // Arrange
        string logContent = @"INFO: Line 1
ERROR: Line 2
WARNING: Line 3";
        string instanceName = "test-instance";

        // Act
        var results = LogParser.ParseLogContent(logContent, instanceName).ToList();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(KgsmLogLevel.Info, results[0].Level);
        Assert.Equal(KgsmLogLevel.Error, results[1].Level);
        Assert.Equal(KgsmLogLevel.Warning, results[2].Level);
    }

    [Fact]
    public void ParseLogContent_EmptyLines_SkipsEmptyLines()
    {
        // Arrange
        string logContent = @"INFO: Line 1

ERROR: Line 2

WARNING: Line 3";
        string instanceName = "test-instance";

        // Act
        var results = LogParser.ParseLogContent(logContent, instanceName).ToList();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotEmpty(r.Message));
    }

    [Fact]
    public void ParseLogContent_MixedLineEndings_ParsesCorrectly()
    {
        // Arrange
        string logContent = "INFO: Line 1\nERROR: Line 2\rWARNING: Line 3\r\nDEBUG: Line 4";
        string instanceName = "test-instance";

        // Act
        var results = LogParser.ParseLogContent(logContent, instanceName).ToList();

        // Assert
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void ParseLogLine_PreservesRawLine()
    {
        // Arrange
        string rawLine = "[2024-01-15 10:30:45] [INFO] Test message";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.Equal(rawLine, result.RawLine);
    }

    [Fact]
    public void ParseLogLine_SetsTimestampToNow_WhenNoTimestampPresent()
    {
        // Arrange
        string rawLine = "INFO: Simple message";
        string instanceName = "test-instance";
        var beforeParsing = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);
        var afterParsing = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(result.Timestamp >= beforeParsing && result.Timestamp <= afterParsing);
    }

    [Fact]
    public void ParseLogLine_CaseInsensitiveLevel_ParsesCorrectly()
    {
        // Arrange
        string rawLine = "info: Test message";
        string instanceName = "test-instance";

        // Act
        var result = LogParser.ParseLogLine(rawLine, instanceName);

        // Assert
        Assert.Equal(KgsmLogLevel.Info, result.Level);
    }
}
