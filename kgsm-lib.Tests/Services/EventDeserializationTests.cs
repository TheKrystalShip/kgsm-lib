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
    // Mirrors EventService's deserialize path for a subject the payload does not name as an instance —
    // the same two steps, resolved against the subject-neutral root.
    private static (string EventType, KgsmEventDataBase? Data) DeserializeAny(
        string wireJson, Type targetType)
    {
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(wireJson, KgsmJsonContext.Default.EventWrapper);
        Assert.NotNull(wrapper);

        var data = JsonSerializer.Deserialize(
            wrapper!.Data.GetRawText(), targetType, KgsmJsonContext.Default)
            as KgsmEventDataBase;

        return (wrapper.EventType, data);
    }

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

    // Models the kgsm `_build_event_payload instance_ports_opened factorio-01
    // '34197/udp|27015:27020/tcp'` wire shape: the firewall audit event the kgsm
    // command layer emits after the kgsm-firewall authority opens the ports. Data.Ports
    // is the canonical STRUCTURED array (range-preserving [{start,end,protocol}], the
    // same shape `instances info --json` emits), built via jq --argjson — never an
    // opaque UFW string. Stamped with the caller's actor/origin (here a kgsm-api emit).
    private const string PortsOpenedWireJson = """
        {"EventType":"instance_ports_opened","Data":{"InstanceName":"factorio-01","Ports":[{"start":34197,"end":34197,"protocol":"udp"},{"start":27015,"end":27020,"protocol":"tcp"}]},"Timestamp":"2026-06-16T08:00:00Z","Actor":"discord:tester","Origin":"api","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    // The close event with a single proto-less-expanded port (one entry here for variety).
    private const string PortsClosedWireJson = """
        {"EventType":"instance_ports_closed","Data":{"InstanceName":"factorio-01","Ports":[{"start":7777,"end":7777,"protocol":"tcp"}]},"Timestamp":"2026-06-16T08:00:00Z","Actor":"system","Origin":"api","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    [Fact]
    public void PortsOpenedEvent_DeserializesStructuredRangePreservingPorts()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(PortsOpenedWireJson, typeof(InstancePortsOpenedData));

        Assert.Equal("instance_ports_opened", eventType);
        var opened = Assert.IsType<InstancePortsOpenedData>(data);
        Assert.Equal("factorio-01", opened.InstanceName);

        // Ports bind from the structured wire array, NOT an opaque string — and the
        // tcp range is preserved (start != end), never expanded.
        Assert.Equal(2, opened.Ports.Count);
        Assert.Equal(new PortMapping { Start = 34197, End = 34197, Protocol = "udp" }, opened.Ports[0]);
        Assert.Equal(new PortMapping { Start = 27015, End = 27020, Protocol = "tcp" }, opened.Ports[1]);
    }

    [Fact]
    public void PortsClosedEvent_DeserializesStructuredPorts()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(PortsClosedWireJson, typeof(InstancePortsClosedData));

        Assert.Equal("instance_ports_closed", eventType);
        var closed = Assert.IsType<InstancePortsClosedData>(data);
        Assert.Equal("factorio-01", closed.InstanceName);
        Assert.Single(closed.Ports);
        Assert.Equal(new PortMapping { Start = 7777, End = 7777, Protocol = "tcp" }, closed.Ports[0]);
    }

    // The watchdog's UPnP-open audit event — DISTINCT from instance_ports_opened (router NAT
    // forward, not a ufw rule). Emitted by the resident supervisor after upnpc exits 0, stamped
    // Actor=system / Origin=system (an autonomous daemon action). Same structured Ports shape.
    private const string UpnpOpenedWireJson = """
        {"EventType":"instance_upnp_opened","Data":{"InstanceName":"factorio-01","Ports":[{"start":34197,"end":34197,"protocol":"udp"},{"start":27015,"end":27020,"protocol":"tcp"}]},"Timestamp":"2026-06-20T08:00:00Z","Actor":"system","Origin":"system","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    private const string UpnpClosedWireJson = """
        {"EventType":"instance_upnp_closed","Data":{"InstanceName":"factorio-01","Ports":[{"start":7777,"end":7777,"protocol":"udp"}]},"Timestamp":"2026-06-20T08:00:00Z","Actor":"system","Origin":"system","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    [Fact]
    public void UpnpOpenedEvent_DeserializesStructuredRangePreservingPorts()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(UpnpOpenedWireJson, typeof(InstanceUpnpOpenedData));

        Assert.Equal("instance_upnp_opened", eventType);
        var opened = Assert.IsType<InstanceUpnpOpenedData>(data);
        Assert.Equal("factorio-01", opened.InstanceName);

        // Range preserved (start != end), never pre-expanded — same canonical shape as the
        // firewall ports event, a different (router) fact.
        Assert.Equal(2, opened.Ports.Count);
        Assert.Equal(new PortMapping { Start = 34197, End = 34197, Protocol = "udp" }, opened.Ports[0]);
        Assert.Equal(new PortMapping { Start = 27015, End = 27020, Protocol = "tcp" }, opened.Ports[1]);
    }

    [Fact]
    public void UpnpClosedEvent_DeserializesStructuredPorts()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(UpnpClosedWireJson, typeof(InstanceUpnpClosedData));

        Assert.Equal("instance_upnp_closed", eventType);
        var closed = Assert.IsType<InstanceUpnpClosedData>(data);
        Assert.Equal("factorio-01", closed.InstanceName);
        Assert.Single(closed.Ports);
        Assert.Equal(new PortMapping { Start = 7777, End = 7777, Protocol = "udp" }, closed.Ports[0]);
    }

    // Models the kgsm `_build_event_payload instance_player_joined factorio-01 76561198000000000 haru`
    // wire shape: Data.PlayerId / Data.PlayerName are the out-of-band nullable params (rendered to JSON
    // null when empty by the builder — never an empty string). Forwarded by the watchdog from a
    // container's in-image shim → stamped Actor=system / Origin=system (an autonomous observation).
    private const string PlayerJoinedWireJson = """
        {"EventType":"instance_player_joined","Data":{"InstanceName":"factorio-01","PlayerId":"76561198000000000","PlayerName":"haru"},"Timestamp":"2026-06-20T08:00:00Z","Actor":"system","Origin":"system","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    // The leave event with a NAME-ONLY source: PlayerId is JSON null (the source gave no stable id) —
    // surfaced honestly as null, never a fabricated id. The at-least-one-non-null rule is the shim's job.
    private const string PlayerLeftWireJson = """
        {"EventType":"instance_player_left","Data":{"InstanceName":"factorio-01","PlayerId":null,"PlayerName":"haru"},"Timestamp":"2026-06-20T08:05:00Z","Actor":"system","Origin":"system","Hostname":"hotrod","KGSMVersion":"3.0.0"}
        """;

    [Fact]
    public void PlayerJoinedEvent_DeserializesIdAndName_WithSystemProvenance()
    {
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(PlayerJoinedWireJson, KgsmJsonContext.Default.EventWrapper);
        Assert.NotNull(wrapper);
        // Autonomous observation forwarded by the watchdog: who = system, surface = system.
        Assert.Equal("system", wrapper!.Actor);
        Assert.Equal("system", wrapper.Origin);

        (string eventType, EventDataBase? data) =
            Deserialize(PlayerJoinedWireJson, typeof(InstancePlayerJoinedData));

        Assert.Equal("instance_player_joined", eventType);
        var joined = Assert.IsType<InstancePlayerJoinedData>(data);
        Assert.Equal("factorio-01", joined.InstanceName);
        Assert.Equal("76561198000000000", joined.PlayerId);
        Assert.Equal("haru", joined.PlayerName);
    }

    [Fact]
    public void PlayerLeftEvent_DeserializesNameOnly_NullIdNeverFabricated()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(PlayerLeftWireJson, typeof(InstancePlayerLeftData));

        Assert.Equal("instance_player_left", eventType);
        var left = Assert.IsType<InstancePlayerLeftData>(data);
        Assert.Equal("factorio-01", left.InstanceName);
        // Name-only source: the id is honestly null, not a fabricated value.
        Assert.Null(left.PlayerId);
        Assert.Equal("haru", left.PlayerName);
    }

    // Captured verbatim from the live journal on hotrod (/var/lib/kgsm/events/*.ndjson)
    // after `kgsm instances kick|ban|unban romestead 95.19.50.122` against a running
    // server. Target is the identity the operator supplied — an IP here, because
    // romestead's blueprint declares `kick {ip}`; Command is what the engine resolved
    // and actually delivered.
    private const string PlayerKickedWireJson = """
        {"EventType":"instance_player_kicked","Data":{"InstanceName":"romestead","Target":"95.19.50.122","Command":"kick 95.19.50.122"},"Timestamp":"2026-08-04T20:31:00Z","Actor":"heisen","Origin":"cli","Hostname":"hotrod","KGSMVersion":"3.7.0-rc1"}
        """;

    private const string PlayerBannedWireJson = """
        {"EventType":"instance_player_banned","Data":{"InstanceName":"romestead","Target":"95.19.50.122","Command":"ban 95.19.50.122"},"Timestamp":"2026-08-04T20:31:02Z","Actor":"heisen","Origin":"cli","Hostname":"hotrod","KGSMVersion":"3.7.0-rc1"}
        """;

    private const string PlayerUnbannedWireJson = """
        {"EventType":"instance_player_unbanned","Data":{"InstanceName":"romestead","Target":"95.19.50.122","Command":"unban 95.19.50.122"},"Timestamp":"2026-08-04T20:31:04Z","Actor":"heisen","Origin":"cli","Hostname":"hotrod","KGSMVersion":"3.7.0-rc1"}
        """;

    [Fact]
    public void PlayerKickedEvent_CarriesTargetAndResolvedCommand()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(PlayerKickedWireJson, typeof(InstancePlayerKickedData));

        Assert.Equal("instance_player_kicked", eventType);
        var kicked = Assert.IsType<InstancePlayerKickedData>(data);
        Assert.Equal("romestead", kicked.InstanceName);
        Assert.Equal("95.19.50.122", kicked.Target);
        Assert.Equal("kick 95.19.50.122", kicked.Command);
    }

    [Fact]
    public void PlayerBannedEvent_CarriesTargetAndResolvedCommand()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(PlayerBannedWireJson, typeof(InstancePlayerBannedData));

        Assert.Equal("instance_player_banned", eventType);
        var banned = Assert.IsType<InstancePlayerBannedData>(data);
        Assert.Equal("95.19.50.122", banned.Target);
        Assert.Equal("ban 95.19.50.122", banned.Command);
    }

    [Fact]
    public void PlayerUnbannedEvent_CarriesTargetAndResolvedCommand()
    {
        (string eventType, EventDataBase? data) =
            Deserialize(PlayerUnbannedWireJson, typeof(InstancePlayerUnbannedData));

        Assert.Equal("instance_player_unbanned", eventType);
        var unbanned = Assert.IsType<InstancePlayerUnbannedData>(data);
        Assert.Equal("95.19.50.122", unbanned.Target);
        Assert.Equal("unban 95.19.50.122", unbanned.Command);
    }

    [Fact]
    public void ModerationEvents_CarryOperatorProvenance_NotSystem()
    {
        // Unlike the player join/leave pair (autonomous observations stamped system),
        // a moderation event is a human action and must stay attributable to whoever
        // caused it.
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(PlayerBannedWireJson, KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(wrapper);
        Assert.Equal("heisen", wrapper!.Actor);
        Assert.Equal("cli", wrapper.Origin);
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
        // surface at runtime. This auto-discovers every concrete event data type
        // and asserts each resolves through the source-gen context. It walks from
        // KgsmEventDataBase, the subject-neutral root, so a non-instance event
        // (blueprint-scoped, and whatever subject comes next) is covered too — an
        // instance-only walk would leave exactly those unguarded.
        static bool IsRegistered(Type t)
        {
            try { return KgsmJsonContext.Default.GetTypeInfo(t) is not null; }
            catch { return false; }
        }

        var unregistered = typeof(KgsmEventDataBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(KgsmEventDataBase)) && !t.IsAbstract)
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
            new Mock<IEventSource>().Object,
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
        var missing = typeof(KgsmEventDataBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(KgsmEventDataBase)) && !t.IsAbstract)
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

    // ---- blueprint events: the first subject that is not an instance -------------------------------

    // All three captured verbatim from a live `kgsm events emit` through the socket transport.
    private const string BlueprintUpdatedWireJson = """
        {"EventType":"blueprint_updated","Data":{"BlueprintName":"terraria","Tier":"user","OverridesSystem":true,"Runtime":"native"},"Timestamp":"2026-07-27T18:46:50Z","Actor":"discord:987654321","Origin":"ui","Hostname":"hotrod","KGSMVersion":"3.1.2-rc9"}
        """;

    private const string BlueprintRemovedWireJson = """
        {"EventType":"blueprint_removed","Data":{"BlueprintName":"teamfortress2","Tier":"user","RevertedToSystem":false},"Timestamp":"2026-07-27T18:46:51Z","Actor":"user:heisen","Origin":"api","Hostname":"hotrod","KGSMVersion":"3.1.2-rc9"}
        """;

    // Emitted with no runtime argument and no provenance env vars: the engine renders the unknown
    // runtime and the undeclared origin as JSON null, and falls back to the invoking OS user for the actor.
    private const string BlueprintCreatedWireJson = """
        {"EventType":"blueprint_created","Data":{"BlueprintName":"odd","Tier":"user","OverridesSystem":false,"Runtime":null},"Timestamp":"2026-07-27T18:46:51Z","Actor":"heisen","Origin":null,"Hostname":"hotrod","KGSMVersion":"3.1.2-rc9"}
        """;

    [Fact]
    public void BlueprintUpdatedEvent_DeserializesWithABlueprintNameNotAnInstanceName()
    {
        (string eventType, KgsmEventDataBase? data) =
            DeserializeAny(BlueprintUpdatedWireJson, typeof(BlueprintUpdatedData));

        Assert.Equal("blueprint_updated", eventType);
        var updated = Assert.IsType<BlueprintUpdatedData>(data);
        Assert.Equal("terraria", updated.BlueprintName);
        Assert.Equal(BlueprintTier.User, updated.Tier);
        Assert.True(updated.OverridesSystem);
        Assert.Equal("native", updated.Runtime);
        // The subject is a blueprint, so there is deliberately no InstanceName to carry — this type
        // sits beside the instance-scoped hierarchy rather than inside it.
        Assert.False(data is EventDataBase);
    }

    [Fact]
    public void BlueprintRemovedEvent_DeserializesRevertedToSystemAsARealBoolean()
    {
        (string eventType, KgsmEventDataBase? data) =
            DeserializeAny(BlueprintRemovedWireJson, typeof(BlueprintRemovedData));

        Assert.Equal("blueprint_removed", eventType);
        var removed = Assert.IsType<BlueprintRemovedData>(data);
        Assert.Equal("teamfortress2", removed.BlueprintName);
        Assert.False(removed.RevertedToSystem); // nothing was restored — the blueprint is gone
    }

    [Fact]
    public void BlueprintCreatedEvent_DeserializesANullRuntimeAsUnknown()
    {
        (string eventType, KgsmEventDataBase? data) =
            DeserializeAny(BlueprintCreatedWireJson, typeof(BlueprintCreatedData));

        Assert.Equal("blueprint_created", eventType);
        var created = Assert.IsType<BlueprintCreatedData>(data);
        Assert.Equal("odd", created.BlueprintName);
        Assert.False(created.OverridesSystem);
        Assert.Null(created.Runtime); // unknown, never defaulted to "native"
    }

    [Fact]
    public void BlueprintEvent_CarriesEnvelopeProvenance()
    {
        EventWrapper? wrapper =
            JsonSerializer.Deserialize(BlueprintCreatedWireJson, KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(wrapper);
        Assert.Equal("heisen", wrapper!.Actor);   // the engine's OS-user fallback
        Assert.Null(wrapper.Origin);              // no surface declared — never fabricated
        Assert.Equal("3.1.2-rc9", wrapper.KgsmVersion);
    }
}
