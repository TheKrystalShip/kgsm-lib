using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Guards the kgsm → kgsm-lib event contract at the wire boundary.
///
/// Event names and payload shapes are maintained by hand in two places that
/// must agree: kgsm's <c>EVENT_CONFIGS</c> registry (bash) and the C#
/// <c>_eventTypeMapping</c> + <see cref="EventDataBase"/> types. They had drifted
/// — <c>instance-restarted</c> and the <c>*-failed</c> events were emitted by
/// kgsm but had no C# type, so they were dropped as "Unknown event type". These
/// tests pin (1) the real wire payloads for the newly-aligned events, captured
/// verbatim from <c>_build_event_payload</c>, and (2) the AOT invariant that
/// every event type is registered in <see cref="KgsmJsonContext"/> — an
/// unregistered type throws at runtime on the reflection-free deserialize path.
/// </summary>
public class EventDeserializationTests
{
    // Mirrors EventService's deserialize path: EventWrapper first, then the
    // typed Data payload, both through the source-generated context.
    private static (string EventType, EventDataBase? Data) Deserialize(
        string wireJson, Type targetType)
    {
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(wireJson, KgsmJsonContext.Default.EventWrapper);
        Assert.NotNull(wrapper);

        var data = JsonSerializer.Deserialize(
            wrapper!.Data.GetRawText(), targetType, KgsmJsonContext.Default)
            as EventDataBase;

        return (wrapper.EventType, data);
    }

    // Captured from an older kgsm `_build_event_payload instance_restarted 7dtd standalone`. The
    // `LifecycleManager` field has since been removed from KGSM's payloads; it is retained here on
    // purpose to prove the lib tolerates (ignores) the legacy field rather than throwing on it.
    private const string RestartedWireJson = """
        {"EventType":"instance_restarted","Data":{"InstanceName":"7dtd","LifecycleManager":"standalone"},"Timestamp":"2026-06-11T21:00:43Z","Hostname":"hotrod","KGSMVersion":"unknown"}
        """;

    // Captured verbatim from kgsm `_build_event_payload instance_download_failed 7dtd`.
    private const string DownloadFailedWireJson = """
        {"EventType":"instance_download_failed","Data":{"InstanceName":"7dtd"},"Timestamp":"2026-06-11T21:00:43Z","Hostname":"hotrod","KGSMVersion":"unknown"}
        """;

    [Fact]
    public void RestartedEvent_Deserializes_IgnoringLegacyLifecycleManager()
    {
        (string eventType, EventDataBase? data) = Deserialize(
            RestartedWireJson, typeof(InstanceRestartedData));

        Assert.Equal("instance_restarted", eventType);
        var restarted = Assert.IsType<InstanceRestartedData>(data);
        Assert.Equal("7dtd", restarted.InstanceName);
        // The legacy `LifecycleManager` field in the payload is unmapped and silently ignored
        // (the property was removed) — deserialization must not throw on it.
    }

    [Fact]
    public void DownloadFailedEvent_DeserializesWithInstanceNameOnly()
    {
        (string eventType, EventDataBase? data) = Deserialize(
            DownloadFailedWireJson, typeof(InstanceDownloadFailedData));

        Assert.Equal("instance_download_failed", eventType);
        var failed = Assert.IsType<InstanceDownloadFailedData>(data);
        Assert.Equal("7dtd", failed.InstanceName);
    }

    // Models the kgsm `_build_event_payload instance_crashed 7dtd 139 2` wire shape: the
    // watchdog crash-restart event, stamped Actor=system / Origin=system (autonomous engine
    // action), carrying the exit code + restart-attempt count as strings (the jq --arg wire
    // form). Reconstructed from the payload builder; the BashEventRegistry conformance test
    // pins the event name against the real sibling kgsm.
    private const string CrashedWireJson = """
        {"EventType":"instance_crashed","Data":{"InstanceName":"7dtd","ExitCode":"139","Restarts":"2"},"Timestamp":"2026-06-15T08:00:00Z","Actor":"system","Origin":"system","Hostname":"hotrod","KGSMVersion":"unknown"}
        """;

    // The give-up event: the supervisor exhausted its retries. ExitCode is the literal
    // "unknown" here — the respawn could not read a code — never a fabricated 0.
    private const string FailedWireJson = """
        {"EventType":"instance_failed","Data":{"InstanceName":"7dtd","ExitCode":"unknown","Restarts":"5"},"Timestamp":"2026-06-15T08:00:00Z","Actor":"system","Origin":"system","Hostname":"hotrod","KGSMVersion":"unknown"}
        """;

    [Fact]
    public void CrashedEvent_DeserializesExitCodeAndRestarts_WithSystemProvenance()
    {
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(CrashedWireJson, KgsmJsonContext.Default.EventWrapper);
        Assert.NotNull(wrapper);
        // Autonomous engine action: who = system, surface = system.
        Assert.Equal("system", wrapper!.Actor);
        Assert.Equal("system", wrapper.Origin);

        (string eventType, EventDataBase? data) =
            Deserialize(CrashedWireJson, typeof(InstanceCrashedData));

        Assert.Equal("instance_crashed", eventType);
        var crashed = Assert.IsType<InstanceCrashedData>(data);
        Assert.Equal("7dtd", crashed.InstanceName);
        Assert.Equal("139", crashed.ExitCode);
        Assert.Equal("2", crashed.Restarts);
    }

    [Fact]
    public void FailedEvent_DeserializesWithUnknownExitCode_NeverFabricated()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(FailedWireJson, typeof(InstanceFailedData));

        Assert.Equal("instance_failed", eventType);
        var failed = Assert.IsType<InstanceFailedData>(data);
        Assert.Equal("7dtd", failed.InstanceName);
        // Honest unknown — the unreadable exit code is "unknown", not a fabricated code.
        Assert.Equal("unknown", failed.ExitCode);
        Assert.Equal("5", failed.Restarts);
    }

    // Models the kgsm `_build_event_payload` wire shape after the actor/timestamp
    // enrichment (reconstructed from a captured emit; JSON is whitespace/order-
    // insensitive): the envelope now carries a top-level Actor alongside Timestamp.
    private const string EnrichedWireJson = """
        {"EventType":"instance_started","Data":{"InstanceName":"factorio-01"},"Timestamp":"2026-06-14T15:39:58Z","Actor":"discord:tester","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    [Fact]
    public void EventWrapper_SurfacesEnvelopeMetadata_FromEnrichedWireJson()
    {
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(EnrichedWireJson, KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(wrapper);
        Assert.Equal("instance_started", wrapper!.EventType);
        Assert.Equal("discord:tester", wrapper.Actor);
        Assert.Equal(
            new DateTimeOffset(2026, 6, 14, 15, 39, 58, TimeSpan.Zero),
            wrapper.Timestamp);
        Assert.Equal("hotrod", wrapper.Hostname);
        // KgsmVersion binds via [JsonPropertyName("KGSMVersion")] despite the casing.
        Assert.Equal("3.0.0", wrapper.KgsmVersion);
    }

    [Fact]
    public void EventWrapper_MissingEnvelopeMetadata_IsNull()
    {
        // A pre-enrichment / minimal payload: the new envelope fields are honestly
        // absent (null), never a fabricated default.
        const string minimalWire =
            """{"EventType":"instance_started","Data":{"InstanceName":"x"}}""";

        EventWrapper? wrapper =
            JsonSerializer.Deserialize(minimalWire, KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(wrapper);
        Assert.Null(wrapper!.Actor);
        Assert.Null(wrapper.Timestamp);
        Assert.Null(wrapper.Hostname);
        Assert.Null(wrapper.KgsmVersion);
    }

    [Fact]
    public void EveryEventDataType_IsRegisteredInJsonContext()
    {
        // The reflection-free deserialize path throws on an unregistered type, so
        // a mapping/type added without a matching [JsonSerializable] would only
        // surface at runtime. This auto-discovers every concrete EventDataBase
        // subclass and asserts each resolves through the source-gen context.
        static bool IsRegistered(Type t)
        {
            try { return KgsmJsonContext.Default.GetTypeInfo(t) is not null; }
            catch { return false; }
        }

        var unregistered = typeof(EventDataBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(EventDataBase)) && !t.IsAbstract)
            .Where(t => !IsRegistered(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(unregistered.Count == 0,
            "Event types missing [JsonSerializable] in KgsmJsonContext: "
            + string.Join(", ", unregistered));
    }

    // Reflects EventService's private name→type dispatch table. The ctor only
    // assigns fields (the listener starts in Initialize(), not here), so a
    // mock-constructed instance is safe and side-effect free.
    private static Dictionary<string, Type> GetEventTypeMapping()
    {
        var svc = new EventService(
            new Mock<IUnixSocketClient>().Object,
            new Mock<ILogger<EventService>>().Object);
        FieldInfo field = typeof(EventService).GetField(
            "_eventTypeMapping", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Dictionary<string, Type>)field.GetValue(svc)!;
    }

    [Fact]
    public void EveryEventDataType_HasAMappingEntry()
    {
        // Guards the (c)-direction the original incident was in: a type + its
        // [JsonSerializable] can exist while the _eventTypeMapping entry is missing,
        // in which case the event is dropped at runtime as "Unknown event type" and
        // every other test still passes. This catches the omission with no external
        // dependency.
        var mapped = new HashSet<Type>(GetEventTypeMapping().Values);
        var missing = typeof(EventDataBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(EventDataBase)) && !t.IsAbstract)
            .Where(t => !mapped.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(missing.Count == 0,
            "Event types absent from EventService._eventTypeMapping (would be dropped "
            + "as 'Unknown event type'): " + string.Join(", ", missing));
    }

    [Fact]
    public void BashEventRegistry_IsSubsetOf_CSharpMapping()
    {
        // The true bash↔C# conformance: every event kgsm can emit (its EVENT_CONFIGS
        // registry) must have a C# mapping entry — catches both a missing entry and a
        // key typo. Reads the sibling kgsm repo when colocated (the tks workspace).
        // xUnit v2 has no dynamic skip, so this no-ops in a standalone kgsm-lib
        // checkout; the always-on EveryEventDataType_HasAMappingEntry covers the same
        // missing-entry incident class without the sibling.
        string? handler = FindKgsmEventsHandler();
        if (handler is null) return;

        HashSet<string> bashEvents = ParseRegisteredBashEvents(handler!);
        Assert.NotEmpty(bashEvents);

        HashSet<string> mappingKeys = GetEventTypeMapping().Keys.ToHashSet();
        var missing = bashEvents.Where(e => !mappingKeys.Contains(e)).OrderBy(e => e).ToList();

        Assert.True(missing.Count == 0,
            "kgsm EVENT_CONFIGS events with no C# _eventTypeMapping entry "
            + "(would be dropped as 'Unknown event type'): " + string.Join(", ", missing));
    }

    private static string? FindKgsmEventsHandler()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(
                dir.FullName, "kgsm", "commands", "handlers", "events.sh");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // Parses the kgsm event registry: resolves the EVENT_INSTANCE_* constants used as
    // EVENT_CONFIGS keys to their underscore wire names (the form C# matches on).
    private static HashSet<string> ParseRegisteredBashEvents(string handlerPath)
    {
        string src = File.ReadAllText(handlerPath);

        var constToValue = new Dictionary<string, string>();
        foreach (System.Text.RegularExpressions.Match m in Regex.Matches(src,
            @"(EVENT_[A-Z_]+)=""([a-z_]+)"""))
        {
            constToValue[m.Groups[1].Value] = m.Groups[2].Value;
        }

        var registered = new HashSet<string>();
        foreach (System.Text.RegularExpressions.Match m in
            Regex.Matches(src, @"\[""\$(EVENT_[A-Z_]+)""\]"))
        {
            if (constToValue.TryGetValue(m.Groups[1].Value, out string? wireName))
                registered.Add(wireName);
        }
        return registered;
    }
}
