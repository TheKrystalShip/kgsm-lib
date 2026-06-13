using TheKrystalShip.KGSM.Core.Models.Enums;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the EventService class — the in-process pub/sub that turns a raw
/// KGSM socket message into a typed, dispatched event. The behavior worth pinning
/// is the full receive route: wire JSON → <see cref="EventWrapper"/> → name→type
/// mapping → typed deserialize → the registered handler. The socket transport
/// itself is mocked (<see cref="IUnixSocketClient"/>); deserialization wire-shapes
/// are covered separately by <see cref="EventDeserializationTests"/>.
/// </summary>
public class EventServiceTests
{
    private readonly Mock<IUnixSocketClient> _mockClient;
    private readonly Mock<ILogger<EventService>> _mockLogger;

    public EventServiceTests()
    {
        _mockClient = new Mock<IUnixSocketClient>();
        _mockClient
            .Setup(c => c.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockLogger = new Mock<ILogger<EventService>>();
    }

    private EventService CreateService() => new(_mockClient.Object, _mockLogger.Object);

    private static string Wire(string eventType, string dataJson) =>
        $$"""
        {"EventType":"{{eventType}}","Data":{{dataJson}},"Timestamp":"2026-06-11T21:00:43Z","Hostname":"hotrod","KGSMVersion":"unknown"}
        """;

    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EventService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EventService(_mockClient.Object, null!));
    }

    [Fact]
    public void RegisterHandler_NullHandler_ThrowsArgumentNullException()
    {
        using EventService svc = CreateService();
        Assert.Throws<ArgumentNullException>(() =>
            svc.RegisterHandler<InstanceStartedData>(null!));
    }

    [Fact]
    public void Initialize_SubscribesToSocketAndStartsListening()
    {
        using EventService svc = CreateService();

        svc.Initialize();

        _mockClient.Verify(c => c.StartListeningAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceivedEvent_WithRegisteredHandler_InvokesHandlerWithTypedData()
    {
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<InstanceStartedData>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterHandler<InstanceStartedData>(data =>
        {
            tcs.TrySetResult(data);
            return Task.CompletedTask;
        });
        svc.Initialize();

        // The payload still carries a legacy `LifecycleManager` field; the lib must ignore it
        // (the property was removed) and still dispatch on InstanceName.
        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd","LifecycleManager":"standalone"}"""));

        InstanceStartedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("7dtd", received.InstanceName);
    }

    [Fact]
    public async Task ReceivedEvent_FailureEventWithInstanceOnly_InvokesHandler()
    {
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<InstanceDownloadFailedData>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterHandler<InstanceDownloadFailedData>(data =>
        {
            tcs.TrySetResult(data);
            return Task.CompletedTask;
        });
        svc.Initialize();

        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_download_failed", """{"InstanceName":"7dtd"}"""));

        InstanceDownloadFailedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("7dtd", received.InstanceName);
    }

    [Fact]
    public void ReceivedEvent_UnknownEventType_DoesNotInvokeHandlerOrThrow()
    {
        using EventService svc = CreateService();
        var invoked = false;
        svc.RegisterHandler<InstanceStartedData>(_ => { invoked = true; return Task.CompletedTask; });
        svc.Initialize();

        // An event name with no _eventTypeMapping entry is logged and dropped, never thrown.
        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_teleported", """{"InstanceName":"7dtd"}"""));

        Assert.False(invoked);
    }

    [Fact]
    public void ReceivedEvent_NoHandlerRegisteredForType_DoesNotThrow()
    {
        using EventService svc = CreateService();
        svc.Initialize();

        // Known event type, but nobody subscribed — must be a no-op, not an error.
        Exception? ex = Record.Exception(() => _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd","LifecycleManager":"standalone"}""")));

        Assert.Null(ex);
    }

    [Fact]
    public void ReceivedEvent_HandlerThrows_ExceptionIsSwallowed()
    {
        using EventService svc = CreateService();
        svc.RegisterHandler<InstanceStartedData>(_ => throw new InvalidOperationException("boom"));
        svc.Initialize();

        // A faulty handler must not bring down the receive loop.
        Exception? ex = Record.Exception(() => _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd","LifecycleManager":"standalone"}""")));

        Assert.Null(ex);
    }

    [Fact]
    public void RegisterHandler_AfterDispose_ThrowsObjectDisposedException()
    {
        EventService svc = CreateService();
        svc.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            svc.RegisterHandler<InstanceStartedData>(_ => Task.CompletedTask));
    }

    [Fact]
    public void Initialize_AfterDispose_ThrowsObjectDisposedException()
    {
        EventService svc = CreateService();
        svc.Dispose();

        Assert.Throws<ObjectDisposedException>(() => svc.Initialize());
    }
}
