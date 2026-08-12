using System.Text.Json;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the federated half of the journal: the producer-aware id encoding, the writer a
/// component uses to record what it did, and the reader that merges every producer's journal into
/// one page.
/// </summary>
/// <remarks>
/// Like the other journal suites these run the real implementations against temporary directories.
/// The journal is ordinary files, so the whole contract — atomic single-line appends, millisecond
/// timestamps, absent-means-null, per-producer coverage, and what a merged page will and will not
/// claim when one producer is missing — is exercised end to end with no fake in the middle.
/// </remarks>
public sealed class EventJournalFederationTests : IDisposable
{
    private readonly string _root;

    public EventJournalFederationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kgsm-federation-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone.
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────

    private string DirectoryFor(string producer)
    {
        string path = Path.Combine(_root, producer);
        Directory.CreateDirectory(path);
        return path;
    }

    private EventJournalWriter CreateWriter(
        string producer, string? version = null, DateTimeOffset? at = null, string? hostname = "testhost")
        => new(
            new EventJournalWriterOptions
            {
                Producer = producer,
                Directory = DirectoryFor(producer),
                ProducerVersion = version,
                Hostname = hostname,
                Clock = at is { } fixed_ ? () => fixed_ : null,
            },
            new Mock<ILogger<EventJournalWriter>>().Object);

    private static ILoggerFactory LoggerFactory()
    {
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        return factory.Object;
    }

    private FederatedEventJournalHistory Federated(params JournalSource[] sources)
        => new(
            sources,
            KgsmOptions.DefaultEventHistoryScanBudgetBytes,
            LoggerFactory(),
            new Mock<ILogger<FederatedEventJournalHistory>>().Object);

    private FederatedEventJournalHistory CreateFederated(params string[] producers)
        => Federated([.. producers.Select(p => new JournalSource(p, Path.Combine(_root, p)))]);

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement;

    private static string SoleLine(string directory)
    {
        string[] files = Directory.GetFiles(directory, "*.ndjson");
        Assert.Single(files);
        string[] lines = File.ReadAllLines(files[0]);
        Assert.Single(lines);
        return lines[0];
    }

    // ── AuditId: the producer-prefixed encoding ──────────────────────────────────────────

    [Fact]
    public void ForPosition_WithProducer_PutsTheProducerFirst()
    {
        string id = AuditId.ForPosition("watchdog", "2026-08-07.ndjson", 1234);

        Assert.Equal("evt_watchdog_2026-08-07_000000001234", id);
    }

    // The whole reason the producer leads: string order must stay (producer, segment, offset), because
    // that is the documented cross-journal tie-break within one timestamp.
    [Fact]
    public void ForPosition_WithProducer_SortsByProducerThenSegmentThenOffset()
    {
        string[] ids =
        [
            AuditId.ForPosition("watchdog", "2026-08-07", 5),
            AuditId.ForPosition("kgsm", "2026-08-08", 1),
            AuditId.ForPosition("kgsm", "2026-08-07", 20),
            AuditId.ForPosition("kgsm", "2026-08-07", 3),
        ];

        string[] sorted = [.. ids.Order(StringComparer.Ordinal)];

        Assert.Equal(
        [
            AuditId.ForPosition("kgsm", "2026-08-07", 3),
            AuditId.ForPosition("kgsm", "2026-08-07", 20),
            AuditId.ForPosition("kgsm", "2026-08-08", 1),
            AuditId.ForPosition("watchdog", "2026-08-07", 5),
        ], sorted);
    }

    [Fact]
    public void ForPosition_WithProducer_RejectsAnIdThatWouldBeAmbiguous()
    {
        // An underscore in the producer would make the id unreadable, since the first underscore is
        // what separates the producer from the segment.
        Assert.Throws<ArgumentException>(() => AuditId.ForPosition("kgsm_watchdog", "2026-08-07", 0));
        Assert.Throws<ArgumentException>(() => AuditId.ForPosition("Watchdog", "2026-08-07", 0));
        Assert.Throws<ArgumentException>(() => AuditId.ForPosition("", "2026-08-07", 0));
    }

    [Fact]
    public void TryParseProducerPosition_RoundTripsAPrefixedId()
    {
        string id = AuditId.ForPosition("monitor", "2026-08-07.ndjson", 42);

        Assert.True(AuditId.TryParseProducerPosition(id, out string producer, out string segment, out long offset));
        Assert.Equal("monitor", producer);
        Assert.Equal("2026-08-07", segment);
        Assert.Equal(42, offset);
    }

    [Fact]
    public void TryParseProducerPosition_SaysNothingForAnUnprefixedOrContentId()
    {
        // An unprefixed position id names no producer, and a content-derived one names no position.
        // Both must report false rather than guessing — the caller has to handle "does not say".
        Assert.False(AuditId.TryParseProducerPosition(
            AuditId.ForPosition("2026-08-07", 42), out _, out _, out _));

        Assert.False(AuditId.TryParseProducerPosition("evt_0123456789abcdef", out _, out _, out _));
    }

    [Fact]
    public void TryParseProducerPosition_FailsClosedOnAnUnconventionalSegmentName()
    {
        // "evt_my_segment_000000000042" is an unprefixed id over a segment whose name holds an
        // underscore. Reading "my" as a producer would be a confident wrong answer, so the
        // date-shaped check rejects it.
        Assert.False(AuditId.TryParseProducerPosition(
            AuditId.ForPosition("my_segment", 42), out _, out _, out _));
    }

    [Fact]
    public void UnprefixedForPosition_IsUnchanged()
    {
        // The engine's ids must not move: the two-argument overload is what ships until a consumer
        // deliberately switches, and existing readers compute this exact value.
        Assert.Equal("evt_2026-08-07_000000001234", AuditId.ForPosition("2026-08-07.ndjson", 1234));
    }

    // ── JournalProducer ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("kgsm", true)]
    [InlineData("kgsm-firewall", true)]
    [InlineData("monitor2", true)]
    [InlineData("kgsm_firewall", false)]
    [InlineData("KGSM", false)]
    [InlineData("has space", false)]
    [InlineData("../etc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void JournalProducer_IsValid_AcceptsOnlyUnambiguousIds(string? producer, bool expected)
        => Assert.Equal(expected, JournalProducer.IsValid(producer));

    // ── The writer ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Writer_AppendsOneWholeLinePerEvent()
    {
        EventJournalWriter writer = CreateWriter("watchdog");

        Assert.True(await writer.AppendAsync("instance_ready", Payload("""{"InstanceName":"Ketchup"}""")));
        Assert.True(await writer.AppendAsync("instance_ready", Payload("""{"InstanceName":"Terra"}""")));

        string[] files = Directory.GetFiles(DirectoryFor("watchdog"), "*.ndjson");
        Assert.Single(files);

        // One line per event is the contract every reader's byte-offset cursor depends on.
        string[] lines = File.ReadAllLines(files[0]);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, l => Assert.StartsWith("{", l, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Writer_StampsSchemaVersionAndMillisecondTimestamp()
    {
        var at = new DateTimeOffset(2026, 8, 12, 3, 2, 24, 117, TimeSpan.Zero);
        EventJournalWriter writer = CreateWriter("watchdog", version: "1.8.2", at: at);

        await writer.AppendAsync("instance_ready", Payload("""{"InstanceName":"Ketchup"}"""));

        string line = SoleLine(DirectoryFor("watchdog"));
        EventWrapper? envelope = JsonSerializer.Deserialize(line, KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(envelope);
        Assert.Equal(1, envelope!.SchemaVersion);
        Assert.Equal("instance_ready", envelope.EventType);
        Assert.Equal("1.8.2", envelope.ProducerVersion);
        Assert.Equal("1.8.2", envelope.EmittingVersion);

        // Millisecond precision is what keeps the merge of several journals orderable inside a second.
        Assert.Contains(".117Z", line, StringComparison.Ordinal);
        Assert.Equal(at, envelope.Timestamp);
    }

    [Fact]
    public async Task Writer_OmitsWhatItWasNotTold()
    {
        // Absent means null to every reader, so an unknown actor or origin is left out rather than
        // written as an explicit null — and never filled in with a plausible substitute.
        EventJournalWriter writer = CreateWriter("watchdog", hostname: null);

        await writer.AppendAsync("instance_ready", Payload("""{"InstanceName":"Ketchup"}"""));

        string line = SoleLine(DirectoryFor("watchdog"));
        Assert.DoesNotContain("Actor", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Origin", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Hostname", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ProducerVersion", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writer_LeavesTheCorrelationFieldsAbsent()
    {
        // The envelope reserves them; nothing populates them until the correlation work. A writer
        // emitting an empty OpId would be the first producer to set a precedent by accident.
        EventJournalWriter writer = CreateWriter("watchdog");

        await writer.AppendAsync("instance_ready", Payload("""{"InstanceName":"Ketchup"}"""));

        string line = SoleLine(DirectoryFor("watchdog"));
        Assert.DoesNotContain("OpId", line, StringComparison.Ordinal);
        Assert.DoesNotContain("RunId", line, StringComparison.Ordinal);
        Assert.DoesNotContain("During", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writer_KeepsAMultiLinePayloadOnOneLine()
    {
        // A pretty-printed payload would otherwise break the one-event-per-line contract; composing
        // through Utf8JsonWriter re-serializes it compact, and a newline inside a string is escaped.
        EventJournalWriter writer = CreateWriter("watchdog");

        await writer.AppendAsync(
            "instance_ready",
            Payload("{\n  \"InstanceName\": \"Ket\\nchup\"\n}"));

        string[] lines = File.ReadAllLines(Directory.GetFiles(DirectoryFor("watchdog"), "*.ndjson")[0]);
        Assert.Single(lines);
    }

    [Fact]
    public async Task Writer_RefusesAPayloadThatIsNotAnObject()
    {
        EventJournalWriter writer = CreateWriter("watchdog");

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await writer.AppendAsync("instance_ready", Payload("[1,2,3]")));
    }

    [Fact]
    public void Writer_DefaultsItsDirectoryFromItsProducer()
        => Assert.Equal("/var/lib/kgsm-monitor/events",
            EventJournalWriterOptions.DefaultDirectoryFor("kgsm-monitor"));

    [Fact]
    public void Writer_RefusesAnUnusableProducer()
        => Assert.Throws<ArgumentException>(() => new EventJournalWriter(
            new EventJournalWriterOptions { Producer = "Not_Valid" },
            new Mock<ILogger<EventJournalWriter>>().Object));

    // ── Writer → reader round trip ───────────────────────────────────────────────────────

    [Fact]
    public async Task WhatTheWriterWrites_TheHistoryReaderReadsBack()
    {
        EventJournalWriter writer = CreateWriter("watchdog", version: "1.8.2");
        await writer.AppendAsync(
            "instance_ready", Payload("""{"InstanceName":"Ketchup"}"""),
            actor: "system:watchdog", origin: "system");

        FederatedEventJournalHistory history = CreateFederated("watchdog");
        EventHistoryPage page = await history.QueryAsync(new EventHistoryQuery());

        EventHistoryEntry entry = Assert.Single(page.Events);
        Assert.Equal("instance_ready", entry.Type);
        Assert.Equal("Ketchup", entry.Instance);
        Assert.Equal("system:watchdog", entry.Actor);
        Assert.Equal("system", entry.Origin);

        // The producer is stamped from the journal the line was read from, never from the line.
        Assert.Equal("watchdog", entry.Producer);
        Assert.StartsWith("evt_watchdog_", entry.Id, StringComparison.Ordinal);

        // Reserved and unpopulated.
        Assert.Null(entry.OpId);
        Assert.Null(entry.RunId);
        Assert.Null(entry.During);
    }

    // ── The federated reader ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Federated_MergesEveryProducerNewestFirst()
    {
        var t0 = new DateTimeOffset(2026, 8, 12, 3, 2, 23, 100, TimeSpan.Zero);

        await CreateWriter("kgsm", at: t0).AppendAsync("instance_started", Payload("""{"InstanceName":"Ketchup"}"""));
        await CreateWriter("watchdog", at: t0.AddSeconds(1)).AppendAsync("instance_ports_opened", Payload("""{"InstanceName":"Ketchup"}"""));
        await CreateWriter("monitor", at: t0.AddSeconds(4)).AppendAsync("host_threshold_breach", Payload("""{"InstanceName":"Ketchup"}"""));

        EventHistoryPage page = await CreateFederated("kgsm", "watchdog", "monitor")
            .QueryAsync(new EventHistoryQuery());

        Assert.Equal(
            ["host_threshold_breach", "instance_ports_opened", "instance_started"],
            page.Events.Select(e => e.Type));

        Assert.Equal(["monitor", "watchdog", "kgsm"], page.Events.Select(e => e.Producer));
    }

    [Fact]
    public async Task Federated_OrdersDeterministicallyWithinOneInstant()
    {
        // Two producers appending in the same millisecond cannot be truly ordered by any host-local
        // mechanism. What matters is that every reader agrees, which the producer-prefixed id gives.
        var at = new DateTimeOffset(2026, 8, 12, 3, 2, 24, 117, TimeSpan.Zero);

        await CreateWriter("watchdog", at: at).AppendAsync("instance_ports_opened", Payload("""{"InstanceName":"K"}"""));
        await CreateWriter("kgsm", at: at).AppendAsync("instance_started", Payload("""{"InstanceName":"K"}"""));

        EventHistoryPage first = await CreateFederated("kgsm", "watchdog").QueryAsync(new EventHistoryQuery());
        EventHistoryPage second = await CreateFederated("watchdog", "kgsm").QueryAsync(new EventHistoryQuery());

        // Same order regardless of the order the journals were configured in.
        Assert.Equal(first.Events.Select(e => e.Id), second.Events.Select(e => e.Id));
        Assert.Equal(["watchdog", "kgsm"], first.Events.Select(e => e.Producer));
    }

    [Fact]
    public async Task Federated_ReportsAnAbsentProducerWithoutEmptyingThePage()
    {
        await CreateWriter("kgsm").AppendAsync("instance_started", Payload("""{"InstanceName":"Ketchup"}"""));

        // "monitor" is configured but its journal was never created — the leaf is not installed.
        FederatedEventJournalHistory history = Federated(
            new JournalSource("kgsm", Path.Combine(_root, "kgsm")),
            new JournalSource("monitor", Path.Combine(_root, "monitor")));

        EventHistoryPage page = await history.QueryAsync(new EventHistoryQuery());

        // The page still answers, and says which producer could not be read — a degraded answer
        // rather than an empty one or a silent omission.
        Assert.True(page.JournalReadable);
        Assert.Single(page.Events);

        Assert.NotNull(page.Journals);
        Assert.True(page.Journals!.Single(j => j.Producer == "kgsm").Readable);
        Assert.False(page.Journals!.Single(j => j.Producer == "monitor").Readable);
    }

    [Fact]
    public async Task Federated_CoverageIsTheNewestFloor()
    {
        // kgsm can answer from further back than the monitor. The merged history can only answer
        // COMPLETELY from the later of the two: reporting the older floor would present a window
        // the monitor cannot cover as full coverage.
        var older = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);

        await CreateWriter("kgsm", at: older).AppendAsync("instance_started", Payload("""{"InstanceName":"K"}"""));
        await CreateWriter("monitor", at: newer).AppendAsync("host_threshold_breach", Payload("""{"InstanceName":"K"}"""));

        EventHistoryPage page = await CreateFederated("kgsm", "monitor").QueryAsync(new EventHistoryQuery());

        Assert.Equal(newer, page.CoverageFrom);
        Assert.Equal(older, page.Journals!.Single(j => j.Producer == "kgsm").CoverageFrom);
        Assert.Equal(newer, page.Journals!.Single(j => j.Producer == "monitor").CoverageFrom);
    }

    [Fact]
    public async Task Federated_PagesAcrossProducersWithOneCursor()
    {
        var t0 = new DateTimeOffset(2026, 8, 12, 3, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < 3; i++)
        {
            await CreateWriter("kgsm", at: t0.AddSeconds(i * 2))
                .AppendAsync("instance_started", Payload($$"""{"InstanceName":"k{{i}}"}"""));
            await CreateWriter("watchdog", at: t0.AddSeconds(i * 2 + 1))
                .AppendAsync("instance_ready", Payload($$"""{"InstanceName":"k{{i}}"}"""));
        }

        FederatedEventJournalHistory history = CreateFederated("kgsm", "watchdog");

        EventHistoryPage first = await history.QueryAsync(new EventHistoryQuery { Limit = 4 });
        Assert.Equal(4, first.Events.Count);
        Assert.NotNull(first.NextCursorTsMs);

        EventHistoryPage second = await history.QueryAsync(new EventHistoryQuery
        {
            Limit = 4,
            BeforeTsMs = first.NextCursorTsMs,
            BeforeId = first.NextCursorId,
        });

        // Six events over two journals, paged 4 then 2, with nothing repeated or skipped.
        Assert.Equal(2, second.Events.Count);
        Assert.Empty(first.Events.Select(e => e.Id).Intersect(second.Events.Select(e => e.Id)));
    }

    [Fact]
    public async Task Federated_WithNoJournalsIsUnreadableRatherThanEmpty()
    {
        EventHistoryPage page = await new FederatedEventJournalHistory(
                [],
                KgsmOptions.DefaultEventHistoryScanBudgetBytes,
                new Mock<ILoggerFactory>().Object,
                new Mock<ILogger<FederatedEventJournalHistory>>().Object)
            .QueryAsync(new EventHistoryQuery());

        Assert.False(page.JournalReadable);
        Assert.Empty(page.Events);
    }

    [Fact]
    public void Federated_RefusesTwoJournalsClaimingOneProducer()
    {
        // Two journals for one producer would make the ids derived from them collide, which is the
        // one thing the producer prefix exists to prevent.
        JournalSource[] sources = [new("kgsm", "/a"), new("kgsm", "/b")];

        Assert.Throws<ArgumentException>(() => new FederatedEventJournalHistory(
            sources,
            KgsmOptions.DefaultEventHistoryScanBudgetBytes,
            new Mock<ILoggerFactory>().Object,
            new Mock<ILogger<FederatedEventJournalHistory>>().Object));
    }

    // ── v0 compatibility ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AV0LineStillReads()
    {
        // Written before V, ProducerVersion or millisecond timestamps existed. These stay on disk for
        // a full retention window, so the reader must answer for them without a migration.
        string directory = DirectoryFor("kgsm");
        await File.WriteAllTextAsync(
            Path.Combine(directory, "2026-08-12.ndjson"),
            """{"EventType":"instance_started","Data":{"InstanceName":"Ketchup"},"Timestamp":"2026-08-12T03:02:23Z","Actor":"discord:heisen9386","Origin":"ui","Hostname":"hotrod","KGSMVersion":"3.12.0-rc18"}"""
                + "\n");

        EventHistoryPage page = await CreateFederated("kgsm").QueryAsync(new EventHistoryQuery());

        EventHistoryEntry entry = Assert.Single(page.Events);
        Assert.Equal("instance_started", entry.Type);
        Assert.Equal("Ketchup", entry.Instance);
        Assert.Equal("discord:heisen9386", entry.Actor);
        Assert.Equal("kgsm", entry.Producer);
    }

    [Fact]
    public void V0VersionSpellingFallsBackToTheEngineField()
    {
        EventWrapper? v0 = JsonSerializer.Deserialize(
            """{"EventType":"instance_started","Data":{},"KGSMVersion":"3.12.0-rc18"}""",
            KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(v0);
        Assert.Null(v0!.SchemaVersion);
        Assert.Null(v0.ProducerVersion);
        Assert.Equal("3.12.0-rc18", v0.EmittingVersion);
    }

    [Fact]
    public void A_v1EnvelopeDeserializesItsReservedFields()
    {
        // Nothing writes these yet, but a reader meeting them must not drop them — the correlation
        // work populates them without another envelope change.
        EventWrapper? v1 = JsonSerializer.Deserialize(
            """{"V":1,"EventType":"instance_ready","Data":{},"OpId":"9f3c","RunId":"7a1b","During":["abcd","ef01"]}""",
            KgsmJsonContext.Default.EventWrapper);

        Assert.NotNull(v1);
        Assert.Equal(1, v1!.SchemaVersion);
        Assert.Equal("9f3c", v1.OpId);
        Assert.Equal("7a1b", v1.RunId);
        Assert.Equal(["abcd", "ef01"], v1.During!);
    }

    // ── The per-producer cursor store ────────────────────────────────────────────────────

    [Fact]
    public async Task FederatedCursorStore_KeepsAPositionPerProducer()
    {
        string path = Path.Combine(_root, "cursors.json");
        var store = new FileFederatedEventCursorStore(
            path, new Mock<ILogger<FileFederatedEventCursorStore>>().Object);

        Assert.Null(await store.LoadAsync("kgsm"));

        await store.SaveAsync("kgsm", new EventCursor { Segment = "2026-08-12.ndjson", Offset = 400 });
        await store.SaveAsync("watchdog", new EventCursor { Segment = "2026-08-12.ndjson", Offset = 900 });

        // Saving one producer's position must not drop another's — a read-modify-write on one file.
        Assert.Equal(400, (await store.LoadAsync("kgsm"))!.Offset);
        Assert.Equal(900, (await store.LoadAsync("watchdog"))!.Offset);

        // A producer with no entry is a cold start for that journal alone.
        Assert.Null(await store.LoadAsync("monitor"));
    }

    [Fact]
    public async Task FederatedCursorStore_SurvivesAReload()
    {
        string path = Path.Combine(_root, "cursors.json");

        await new FileFederatedEventCursorStore(path, new Mock<ILogger<FileFederatedEventCursorStore>>().Object)
            .SaveAsync("kgsm", new EventCursor { Segment = "2026-08-12.ndjson", Offset = 77 });

        EventCursor? reloaded = await new FileFederatedEventCursorStore(
                path, new Mock<ILogger<FileFederatedEventCursorStore>>().Object)
            .LoadAsync("kgsm");

        Assert.Equal(77, reloaded!.Offset);
    }

    [Fact]
    public async Task FederatedCursorStore_TreatsACorruptMapAsAColdStart()
    {
        string path = Path.Combine(_root, "cursors.json");
        await File.WriteAllTextAsync(path, "{ not json");

        var store = new FileFederatedEventCursorStore(
            path, new Mock<ILogger<FileFederatedEventCursorStore>>().Object);

        Assert.Null(await store.LoadAsync("kgsm"));
    }
}
