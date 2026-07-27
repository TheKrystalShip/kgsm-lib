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
    public async Task Initialize_SubscribesToSocketAndStartsListening()
    {
        // Initialize starts the listener on a background task and returns without awaiting it, so the
        // call may not have landed by the time Initialize returns — assert it eventually does rather
        // than assuming the scheduler ran it first.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockClient
            .Setup(c => c.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(() => { started.TrySetResult(); return Task.CompletedTask; });

        using EventService svc = CreateService();

        svc.Initialize();

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
    public async Task ReceivedEvent_ConfigChanged_BindsInstanceNameAndKey()
    {
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<InstanceConfigChangedData>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterHandler<InstanceConfigChangedData>(data =>
        {
            tcs.TrySetResult(data);
            return Task.CompletedTask;
        });
        svc.Initialize();

        // Verbatim wire shape captured live: Data is exactly { InstanceName, Key } — both
        // always-present non-null strings. The changed VALUE is never carried (secret
        // hygiene), so the payload only names the key.
        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_config_changed", """{"InstanceName":"factorio-test","Key":"rcon_password"}"""));

        InstanceConfigChangedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("factorio-test", received.InstanceName);
        Assert.Equal("rcon_password", received.Key);
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
    public async Task ReceivedEvent_CopiesEnvelopeActorAndTimestampOntoData()
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

        // Actor/Timestamp live at the top level of the envelope, not inside Data; the
        // service must copy them onto the data object so the handler sees who+when.
        const string wire = """
            {"EventType":"instance_started","Data":{"InstanceName":"7dtd"},"Timestamp":"2026-06-14T15:39:58Z","Actor":"discord:tester","Hostname":"hotrod","KGSMVersion":"3.0.0"}
            """;
        _mockClient.Raise(c => c.EventReceived += null, wire);

        InstanceStartedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("7dtd", received.InstanceName);
        Assert.Equal("discord:tester", received.Actor);
        Assert.Equal(
            new DateTimeOffset(2026, 6, 14, 15, 39, 58, TimeSpan.Zero),
            received.Timestamp);
    }

    [Fact]
    public async Task ReceivedEvent_NoActorOnWire_LeavesActorNull()
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

        // Wire() carries Timestamp but no Actor — the absent actor stays null (never
        // a fabricated identity), while the present timestamp is still copied through.
        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd"}"""));

        InstanceStartedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(received.Actor);
        Assert.NotNull(received.Timestamp);
    }

    [Fact]
    public async Task ReceivedEvent_CopiesEnvelopeOriginOntoData()
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

        // Origin (the driving surface) lives at the top level of the envelope alongside
        // Actor; the service must copy it onto the data object so a handler sees
        // through-which-surface, not just who.
        const string wire = """
            {"EventType":"instance_started","Data":{"InstanceName":"7dtd"},"Actor":"discord:tester","Origin":"assistant","Hostname":"hotrod","KGSMVersion":"3.0.0"}
            """;
        _mockClient.Raise(c => c.EventReceived += null, wire);

        InstanceStartedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("assistant", received.Origin);
        Assert.Equal("discord:tester", received.Actor);
    }

    [Fact]
    public async Task ReceivedEvent_NoOriginOnWire_LeavesOriginNull()
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

        // A bare CLI invocation declares no surface — the absent origin stays null,
        // never a fabricated surface (mirrors the absent-actor contract above).
        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd"}"""));

        InstanceStartedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(received.Origin);
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
    public async Task RegisterRawHandler_KnownEventType_ReceivesFullWrapper()
    {
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<EventWrapper>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterRawHandler(wrapper =>
        {
            tcs.TrySetResult(wrapper);
            return Task.CompletedTask;
        });
        svc.Initialize();

        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd"}"""));

        EventWrapper received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("instance_started", received.EventType);
        Assert.Equal("hotrod", received.Hostname);
    }

    [Fact]
    public async Task RegisterRawHandler_UnknownEventType_StillReceivesWrapper()
    {
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<EventWrapper>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var typedInvoked = false;

        svc.RegisterHandler<InstanceStartedData>(_ => { typedInvoked = true; return Task.CompletedTask; });
        svc.RegisterRawHandler(wrapper =>
        {
            tcs.TrySetResult(wrapper);
            return Task.CompletedTask;
        });
        svc.Initialize();

        // "instance_teleported" has no _eventTypeMapping entry — the raw handler is the
        // whole point of a catch-all audit trail, so it must still fire.
        _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_teleported", """{"InstanceName":"7dtd"}"""));

        EventWrapper received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("instance_teleported", received.EventType);
        Assert.False(typedInvoked);
    }

    [Fact]
    public async Task RegisterRawHandler_ThrowingHandler_DoesNotBlockOtherRawOrTypedHandlers()
    {
        using EventService svc = CreateService();
        var secondRawTcs = new TaskCompletionSource<EventWrapper>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var typedTcs = new TaskCompletionSource<InstanceStartedData>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterRawHandler(_ => throw new InvalidOperationException("boom"));
        svc.RegisterRawHandler(wrapper =>
        {
            secondRawTcs.TrySetResult(wrapper);
            return Task.CompletedTask;
        });
        svc.RegisterHandler<InstanceStartedData>(data =>
        {
            typedTcs.TrySetResult(data);
            return Task.CompletedTask;
        });
        svc.Initialize();

        Exception? ex = Record.Exception(() => _mockClient.Raise(c => c.EventReceived += null,
            Wire("instance_started", """{"InstanceName":"7dtd"}""")));

        Assert.Null(ex);
        EventWrapper secondRaw = await secondRawTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("instance_started", secondRaw.EventType);
        InstanceStartedData typed = await typedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("7dtd", typed.InstanceName);
    }

    [Fact]
    public void RegisterRawHandler_NullHandler_ThrowsArgumentNullException()
    {
        using EventService svc = CreateService();
        Assert.Throws<ArgumentNullException>(() => svc.RegisterRawHandler(null!));
    }

    [Fact]
    public void RegisterRawHandler_AfterDispose_ThrowsObjectDisposedException()
    {
        EventService svc = CreateService();
        svc.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            svc.RegisterRawHandler(_ => Task.CompletedTask));
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

    [Fact]
    public async Task ReceivedEvent_BlueprintUpdated_DispatchesToABlueprintScopedHandler()
    {
        // The full route for a subject that is NOT an instance: wire → envelope → name→type →
        // typed deserialize → handler. Nothing about the dispatch path is instance-specific.
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<BlueprintUpdatedData>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterHandler<BlueprintUpdatedData>(data =>
        {
            tcs.TrySetResult(data);
            return Task.CompletedTask;
        });
        svc.Initialize();

        _mockClient.Raise(c => c.EventReceived += null,
            Wire("blueprint_updated",
                """{"BlueprintName":"terraria","Tier":"user","OverridesSystem":true,"Runtime":"native"}"""));

        BlueprintUpdatedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("terraria", received.BlueprintName);
        Assert.Equal(BlueprintTier.User, received.Tier);
        Assert.True(received.OverridesSystem);
    }

    [Fact]
    public async Task ReceivedEvent_BlueprintRemoved_CarriesEnvelopeProvenanceOntoTheData()
    {
        // The envelope's who/when/through-what is stamped onto the data object by EventService, and
        // that now happens on the subject-neutral root — so a blueprint handler sees it too.
        using EventService svc = CreateService();
        var tcs = new TaskCompletionSource<BlueprintRemovedData>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        svc.RegisterHandler<BlueprintRemovedData>(data =>
        {
            tcs.TrySetResult(data);
            return Task.CompletedTask;
        });
        svc.Initialize();

        _mockClient.Raise(c => c.EventReceived += null,
            """
            {"EventType":"blueprint_removed","Data":{"BlueprintName":"palworld","Tier":"user","RevertedToSystem":true},"Timestamp":"2026-07-27T18:46:51Z","Actor":"user:heisen","Origin":"api","Hostname":"hotrod","KGSMVersion":"3.1.2-rc9"}
            """);

        BlueprintRemovedData received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("palworld", received.BlueprintName);
        Assert.True(received.RevertedToSystem);
        Assert.Equal("user:heisen", received.Actor);
        Assert.Equal("api", received.Origin);
        Assert.NotNull(received.Timestamp);
    }
}
