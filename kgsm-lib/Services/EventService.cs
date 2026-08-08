using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IEventService interface for handling KGSM events.
/// </summary>
/// <remarks>
/// Transport-agnostic by construction: it consumes raw envelopes from an
/// <see cref="IEventSource"/> and never knows whether they arrived over a socket or were read
/// out of the journal. That is what lets a consumer change transport without touching a single
/// handler.
/// </remarks>
public class EventService : IEventService, IAsyncDisposable
{
    private readonly IEventSource _client;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger<EventService> _logger;
    private bool _disposed = false;
    private bool _initialized = false;

    /// <summary>
    /// A dictionary to map event types to their corresponding data types.
    /// This mapping is used to deserialize the event data
    /// based on the event type received from the KGSM Unix Socket.
    /// </summary>
    private readonly Dictionary<string, Type> _eventTypeMapping = new()
    {
        { "instance_created", typeof(InstanceCreatedData) },

        { "instance_directories_created", typeof(InstanceDirectoriesCreatedData) },
        { "instance_files_created", typeof(InstanceFilesCreatedData) },

        { "instance_download_started", typeof(InstanceDownloadStartedData) },
        { "instance_download_finished", typeof(InstanceDownloadFinishedData) },
        { "instance_download_failed", typeof(InstanceDownloadFailedData) },
        { "instance_downloaded", typeof(InstanceDownloadedData) },

        { "instance_deploy_started", typeof(InstanceDeployStartedData) },
        { "instance_deploy_finished", typeof(InstanceDeployFinishedData) },
        { "instance_deploy_failed", typeof(InstanceDeployFailedData) },
        { "instance_deployed", typeof(InstanceDeployedData) },

        { "instance_restart_started", typeof(InstanceRestartStartedData) },
        { "instance_restart_finished", typeof(InstanceRestartFinishedData) },

        { "instance_stop_started", typeof(InstanceStopStartedData) },
        { "instance_stop_finished", typeof(InstanceStopFinishedData) },

        { "instance_update_started", typeof(InstanceUpdateStartedData) },
        { "instance_update_finished", typeof(InstanceUpdateFinishedData) },
        { "instance_updated", typeof(InstanceUpdatedData) },

        { "instance_version_updated", typeof(InstanceVersionUpdatedData) },

        { "instance_installation_started", typeof(InstanceInstallationStartedData) },
        { "instance_installation_finished", typeof(InstanceInstallationFinishedData) },
        { "instance_installed", typeof(InstanceInstalledData) },

        { "instance_started", typeof(InstanceStartedData) },
        { "instance_stopped", typeof(InstanceStoppedData) },
        { "instance_restarted", typeof(InstanceRestartedData) },
        { "instance_crashed", typeof(InstanceCrashedData) },
        { "instance_failed", typeof(InstanceFailedData) },
        { "instance_ready", typeof(InstanceReadyData) },

        { "instance_backup_created", typeof(InstanceBackupCreatedData) },
        { "instance_backup_restored", typeof(InstanceBackupRestoredData) },
        { "instance_backup_deleted", typeof(InstanceBackupDeletedData) },
        { "instance_backups_pruned", typeof(InstanceBackupsPrunedData) },

        { "instance_files_removed", typeof(InstanceFilesRemovedData) },
        { "instance_directories_removed", typeof(InstanceDirectoriesRemovedData) },

        { "instance_removed", typeof(InstanceRemovedData) },

        { "instance_uninstall_started", typeof(InstanceUninstallStartedData) },
        { "instance_uninstall_finished", typeof(InstanceUninstallFinishedData) },
        { "instance_uninstall_failed", typeof(InstanceUninstallFailedData) },
        { "instance_uninstalled", typeof(InstanceUninstalledData) },

        { "instance_ports_opened", typeof(InstancePortsOpenedData) },
        { "instance_ports_closed", typeof(InstancePortsClosedData) },

        { "instance_upnp_opened", typeof(InstanceUpnpOpenedData) },
        { "instance_upnp_closed", typeof(InstanceUpnpClosedData) },
        { "instance_upnp_reasserted", typeof(InstanceUpnpReassertedData) },

        { "instance_player_joined", typeof(InstancePlayerJoinedData) },
        { "instance_player_left", typeof(InstancePlayerLeftData) },

        { "instance_player_kicked", typeof(InstancePlayerKickedData) },
        { "instance_player_banned", typeof(InstancePlayerBannedData) },
        { "instance_player_unbanned", typeof(InstancePlayerUnbannedData) },

        { "instance_config_changed", typeof(InstanceConfigChangedData) },

        { "instance_input_sent", typeof(InstanceInputSentData) },

        { "blueprint_created", typeof(BlueprintCreatedData) },
        { "blueprint_updated", typeof(BlueprintUpdatedData) },
        { "blueprint_removed", typeof(BlueprintRemovedData) }
    };

    /// <summary>
    /// A dictionary to hold event handlers for the different event types.
    /// </summary>
    private readonly Dictionary<Type, Delegate> _eventHandlers = new();

    /// <summary>
    /// Handlers registered via <see cref="RegisterRawHandler"/>, invoked with the full
    /// envelope for every deserialized event regardless of typed dispatch.
    /// </summary>
    private readonly List<Func<EventWrapper, EventPosition, Task>> _rawHandlers = new();

    /// <summary>
    /// Handlers registered via <see cref="RegisterGapHandler"/>, invoked when the transport
    /// reports it could not resume where this consumer left off.
    /// </summary>
    private readonly List<Func<EventJournalGap, Task>> _gapHandlers = new();

    /// <summary>
    /// Initializes a new instance of the EventService class.
    /// </summary>
    /// <param name="client">The transport delivering raw event envelopes.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public EventService(IEventSource client, ILogger<EventService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cts = new CancellationTokenSource();

        _logger.LogDebug("EventService initialized");
    }

    /// <summary>
    /// Finalizer for the EventService class.
    /// </summary>
    ~EventService()
    {
        Dispose(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Try to cancel the CancellationTokenSource
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // CancellationTokenSource already disposed, ignore
            }
            finally
            {
                try
                {
                    _cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }

                try
                {
                    _client.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }

                _disposed = true;
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                // Try to cancel if not already cancelled
                _cts.Cancel();

                // Give some time for async operations to complete
                await Task.Delay(100).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
            finally
            {
                try
                {
                    _cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }

                try
                {
                    if (_client is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        _client.Dispose();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }

                _disposed = true;
            }
        }
    }

    /// <inheritdoc/>
    public void Initialize() => Initialize(startPosition: null);

    /// <inheritdoc/>
    public void Initialize(EventStartPosition startPosition) => Initialize((EventStartPosition?)startPosition);

    /// <summary>
    /// Subscribes to the transport and starts it, optionally overriding where a replayable
    /// transport begins reading.
    /// </summary>
    /// <param name="startPosition">The start position to impose, or null to keep the configured one.</param>
    private void Initialize(EventStartPosition? startPosition)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EventService));

        // Idempotent, and load-bearing that it is. Two callers legitimately reach here — KgsmClient's
        // constructor and whatever the consumer wires — and a second pass would subscribe
        // OnEventReceivedAsync to the transport AGAIN and start a SECOND read loop, so every event
        // arrived N² times: twice-subscribed × two loops = four deliveries of one event, each one
        // driving whatever the consumer does with it (an announcement, a notification, a cache bust).
        // The two loops also raced the journal reader's single cursor. Callers must not have to know
        // who else initializes.
        if (_initialized)
        {
            _logger.LogDebug("Event service already initialized — ignoring the repeat call");
            return;
        }
        _initialized = true;

        _logger.LogInformation("Initializing event service");

        _client.EventReceived += OnEventReceivedAsync;

        if (_client is IEventJournalReader journal)
        {
            if (startPosition.HasValue)
                journal.StartPosition = startPosition.Value;

            journal.GapDetected += OnGapDetectedAsync;
        }
        else if (startPosition.HasValue)
        {
            // Never quietly accept a setting the transport cannot honour: the socket has no
            // history to position within, so a caller asking to replay would get live-only
            // delivery and no indication of why.
            _logger.LogWarning(
                "Start position {StartPosition} ignored: the {Transport} transport delivers only events that arrive while it is listening",
                startPosition.Value, _client.GetType().Name);
        }

        if (_cts.IsCancellationRequested)
            throw new InvalidOperationException("Cannot initialize after disposal.");

        _logger.LogDebug("Starting event listener");

        // Start listening for events asynchronously.
        Task.Run(() => _client.StartListeningAsync(_cts.Token));

        _logger.LogInformation("Event service initialized");
    }

    /// <inheritdoc/>
    public void RegisterHandler<T>(Func<T, Task> handler) where T : KgsmEventDataBase
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EventService));
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        Type eventType = typeof(T);
        _logger.LogDebug("Registering handler for event type {EventType}", eventType.Name);

        _eventHandlers[eventType] = async (KgsmEventDataBase data) => await handler((T)data).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void RegisterRawHandler(Func<EventWrapper, EventPosition, Task> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EventService));
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        _logger.LogDebug("Registering raw event handler");

        _rawHandlers.Add(handler);
    }

    /// <inheritdoc/>
    public void RegisterGapHandler(Func<EventJournalGap, Task> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EventService));
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        _logger.LogDebug("Registering event gap handler");

        _gapHandlers.Add(handler);
    }

    /// <summary>
    /// Fans a transport-reported gap out to every registered gap handler, each isolated so one
    /// throwing handler cannot stop the others or the read loop that raised it.
    /// </summary>
    /// <param name="gap">The reported discontinuity.</param>
    private async Task OnGapDetectedAsync(EventJournalGap gap)
    {
        if (_disposed)
            return;

        _logger.LogWarning(
            "Event stream gap reported at {Segment}+{Offset} ({Reason})",
            gap.LostSegment, gap.LostOffset, gap.Reason);

        foreach (Func<EventJournalGap, Task> gapHandler in _gapHandlers)
        {
            try
            {
                await gapHandler(gap).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event gap handler");
            }
        }
    }

    /// <summary>
    /// Handles one envelope delivered by the transport.
    /// </summary>
    /// <param name="message">The raw event envelope.</param>
    /// <param name="position">Where the envelope sits in the journal, for handlers that key on it.</param>
    private async Task OnEventReceivedAsync(string message, EventPosition position)
    {
        // Ignore events after disposal
        if (_disposed)
            return;

        _logger.LogDebug("Received event message: {MessageLength} bytes", message.Length);

        try
        {
            EventWrapper? eventWrapper = JsonSerializer.Deserialize(message, KgsmJsonContext.Default.EventWrapper);

            if (eventWrapper == null || string.IsNullOrWhiteSpace(eventWrapper.EventType))
            {
                _logger.LogError("Invalid event wrapper received: {Message}", message);
                return;
            }

            _logger.LogDebug("Processing event of type {EventType}", eventWrapper.EventType);

            // Raw handlers see every envelope — known or unknown EventType — before
            // typed dispatch runs, and never suppress it.
            await InvokeRawHandlersAsync(eventWrapper, position).ConfigureAwait(false);

            if (_eventTypeMapping.TryGetValue(eventWrapper.EventType, out var targetType))
            {
                _logger.LogDebug("Deserializing event data to type {TargetType}", targetType.Name);

                KgsmEventDataBase? eventData = DeserializeEventData(targetType, eventWrapper.Data);

                if (eventData == null)
                {
                    _logger.LogWarning("Failed to deserialize event data for type {EventType}", eventWrapper.EventType);
                    return;
                }

                // Carry the envelope's audit metadata (who/when/through-what) onto the data
                // object so handlers see it without a signature change. These live at the
                // top level of the wrapper, not inside Data, so DeserializeEventData never
                // sets them.
                eventData.Timestamp = eventWrapper.Timestamp;
                eventData.Actor = eventWrapper.Actor;
                eventData.Origin = eventWrapper.Origin;

                await InvokeHandlerAsync(eventData).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("Unknown event type received: {EventType}", eventWrapper.EventType);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize event message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event message");
        }
    }

    /// <summary>
    /// Deserializes the event data from JSON to the specified target type.
    /// </summary>
    /// <param name="targetType">The type to deserialize to.</param>
    /// <param name="dataElement">The JSON data to deserialize.</param>
    /// <returns>The deserialized event data.</returns>
    /// <exception cref="ArgumentNullException">Thrown if targetType is null.</exception>
    /// <exception cref="JsonException">Thrown if JSON deserialization fails.</exception>
    /// <exception cref="NotSupportedException">Thrown if the target type is not supported.</exception>
    private KgsmEventDataBase? DeserializeEventData(Type targetType, JsonElement dataElement)
    {
        ArgumentNullException.ThrowIfNull(targetType, nameof(targetType));

        string json = dataElement.GetRawText();

        _logger.LogTrace("Deserializing JSON: {Json}", json);

        var result = JsonSerializer.Deserialize(json, targetType, KgsmJsonContext.Default) as KgsmEventDataBase;

        _logger.LogDebug("Successfully deserialized event data to {TargetType}", targetType.Name);

        return result;
    }

    /// <summary>
    /// Invokes every registered raw handler with the full envelope, independent of
    /// typed dispatch. Each invocation is isolated in its own try/catch so one
    /// throwing (or slow-to-fault) handler can't stop the others or the read loop.
    /// </summary>
    /// <param name="eventWrapper">The deserialized event envelope.</param>
    /// <param name="position">Where the envelope sits in the journal.</param>
    private async Task InvokeRawHandlersAsync(EventWrapper eventWrapper, EventPosition position)
    {
        foreach (Func<EventWrapper, EventPosition, Task> rawHandler in _rawHandlers)
        {
            try
            {
                await rawHandler(eventWrapper, position).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in raw event handler for {EventType}", eventWrapper.EventType);
            }
        }
    }

    /// <summary>
    /// Invokes the registered handler for the given event data.
    /// </summary>
    /// <param name="eventData">The event data to handle.</param>
    private async Task InvokeHandlerAsync(KgsmEventDataBase eventData)
    {
        Type eventType = eventData.GetType();

        if (!_eventHandlers.TryGetValue(eventType, out var handler))
        {
            _logger.LogDebug("No handler registered for event type {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Invoking handler for event type {EventType}", eventType.Name);

        try
        {
            await ((Func<KgsmEventDataBase, Task>)handler).Invoke(eventData).ConfigureAwait(false);

            _logger.LogDebug("Handler for {EventType} completed successfully", eventType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event handler for {EventType}", eventType.Name);
        }
    }
}
