namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the EventManagementService class.
/// </summary>
public class EventManagementServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<EventManagementService>> _mockLogger;
    private readonly EventManagementService _service;

    public EventManagementServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<EventManagementService>>();
        _service = new EventManagementService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EventManagementService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EventManagementService(_mockCommandExecutor.Object, null!));
    }

    // --- GetStatus ---

    [Fact]
    public void GetStatus_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "status" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Socket: enabled\nWebhook: disabled", string.Empty)));

        // Act
        KgsmResult result = _service.GetStatus();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void GetStatus_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "status" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to get event status")));

        // Act
        KgsmResult result = _service.GetStatus();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- TestTransport ---

    [Fact]
    public void TestTransport_NullTransport_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.TestTransport(null!));
    }

    [Fact]
    public void TestTransport_WhitespaceTransport_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.TestTransport("   "));
    }

    [Fact]
    public void TestTransport_InvalidTransport_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.TestTransport("invalid"));
    }

    [Fact]
    public void TestTransport_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string transport = "all";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "test", transport }))))
            .Returns(new KgsmResult(new ProcessResult(0, "All transports passed", string.Empty)));

        // Act
        KgsmResult result = _service.TestTransport(transport);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void TestTransport_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const string transport = "webhook";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "test", transport }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Webhook test failed")));

        // Act
        KgsmResult result = _service.TestTransport(transport);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- Emit ---

    [Fact]
    public void Emit_NullEventType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Emit(null!));
    }

    [Fact]
    public void Emit_WhitespaceEventType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.Emit("   "));
    }

    [Fact]
    public void Emit_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string eventType = "instance-started";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "emit", eventType }))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        // Act
        KgsmResult result = _service.Emit(eventType);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Emit_WithParameters_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const string eventType = "instance-created";
        const string param1 = "my-server";
        const string param2 = "valheim";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "emit", eventType, param1, param2 }))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        // Act
        KgsmResult result = _service.Emit(eventType, param1, param2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- EmitWithProvenance ---

    [Fact]
    public void EmitWithProvenance_NullEventType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.EmitWithProvenance(null!, "system", "system"));
    }

    [Fact]
    public void EmitWithProvenance_StampsActorAndOrigin_OnEnvironmentAndPassesArgs()
    {
        // Arrange — the watchdog crash-event call shape: actor/origin stamped, instance +
        // exit code + restart count as event parameters.
        const string eventType = "instance-crashed";

        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.Is<IReadOnlyDictionary<string, string>>(env =>
                    env.Count == 2 &&
                    env["KGSM_EVENT_ACTOR"] == "system" &&
                    env["KGSM_EVENT_ORIGIN"] == "system"),
                It.Is<string[]>(args => args.SequenceEqual(
                    new[] { "events", "emit", eventType, "my-server", "139", "2" }))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        // Act
        KgsmResult result =
            _service.EmitWithProvenance(eventType, "system", "system", "my-server", "139", "2");

        // Assert — the env overload was taken (provenance present), not the plain Execute.
        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(
            x => x.Execute(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string[]>()),
            Times.Once);
    }

    [Fact]
    public void EmitWithProvenance_OnlyActor_OmitsOriginFromEnvironment()
    {
        // Arrange — a null/empty origin must NOT be set (KGSM keeps its own honest default;
        // no fabricated surface).
        const string eventType = "instance-failed";

        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.Is<IReadOnlyDictionary<string, string>>(env =>
                    env.Count == 1 &&
                    env["KGSM_EVENT_ACTOR"] == "system" &&
                    !env.ContainsKey("KGSM_EVENT_ORIGIN")),
                It.IsAny<string[]>()))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        // Act
        KgsmResult result = _service.EmitWithProvenance(eventType, "system", null, "my-server");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EmitWithProvenance_NoActorOrOrigin_TakesPlainExecutePath()
    {
        // Arrange — with neither stamped, it must fall through to the plain (no-environment)
        // Execute so KGSM applies its own actor/origin fallbacks.
        const string eventType = "instance-crashed";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args => args.SequenceEqual(
                new[] { "events", "emit", eventType, "my-server" }))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        // Act
        KgsmResult result = _service.EmitWithProvenance(eventType, null, null, "my-server");

        // Assert — no environment overload was used.
        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(
            x => x.Execute(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string[]>()),
            Times.Never);
    }

    // --- EnableSocket ---

    [Fact]
    public void EnableSocket_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "socket", "enable" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Socket transport enabled", string.Empty)));

        // Act
        KgsmResult result = _service.EnableSocket();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- DisableSocket ---

    [Fact]
    public void DisableSocket_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "socket", "disable" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Socket transport disabled", string.Empty)));

        // Act
        KgsmResult result = _service.DisableSocket();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- TestSocket ---

    [Fact]
    public void TestSocket_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "socket", "test" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Socket test passed", string.Empty)));

        // Act
        KgsmResult result = _service.TestSocket();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- GetSocketStatus ---

    [Fact]
    public void GetSocketStatus_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "socket", "status" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Socket: enabled", string.Empty)));

        // Act
        KgsmResult result = _service.GetSocketStatus();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- EnableWebhook ---

    [Fact]
    public void EnableWebhook_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "webhook", "enable" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Webhook transport enabled", string.Empty)));

        // Act
        KgsmResult result = _service.EnableWebhook();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- DisableWebhook ---

    [Fact]
    public void DisableWebhook_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "webhook", "disable" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Webhook transport disabled", string.Empty)));

        // Act
        KgsmResult result = _service.DisableWebhook();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- TestWebhook ---

    [Fact]
    public void TestWebhook_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "webhook", "test" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Webhook test passed", string.Empty)));

        // Act
        KgsmResult result = _service.TestWebhook();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    // --- GetWebhookStatus ---

    [Fact]
    public void GetWebhookStatus_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "events", "webhook", "status" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Webhook: disabled", string.Empty)));

        // Act
        KgsmResult result = _service.GetWebhookStatus();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }
}
