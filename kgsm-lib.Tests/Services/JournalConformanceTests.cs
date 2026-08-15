using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheKrystalShip.KGSM.Events;
using TheKrystalShip.KGSM.Extensions;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the rules every producer follows: where it writes, what it calls itself, which version
/// it stamps, and how a line gets recorded.
/// </summary>
/// <remarks>
/// These are the decisions each producer used to make for itself, and the drift they produced is what
/// this suite exists to keep out. The theme throughout is that a misconfigured producer must be
/// <em>loud</em>: a journal written where no reader scans is not reported as broken — it is not found,
/// and a producer with no journal has honestly recorded nothing, so the two are indistinguishable
/// unless something says so at the moment of writing.
/// </remarks>
public sealed class JournalConformanceTests : IDisposable
{
    private readonly string _root;

    public JournalConformanceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kgsm-conformance-tests", Path.GetRandomFileName());
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

    // ── Layout: the writer's path and the reader's conclusion are one rule ───────────────

    [Theory]
    [InlineData("kgsm")]
    [InlineData("kgsm-api")]
    [InlineData("kgsm-watchdog")]
    [InlineData("kgsm-firewall")]
    public void ProducerOf_InvertsDirectoryFor(string producer)
    {
        // The whole point of having one rule: what the writer composes is what the reader derives.
        Assert.Equal(producer, JournalLayout.ProducerOf(JournalLayout.DirectoryFor(producer)));
    }

    [Fact]
    public void DirectoryFor_UsesTheStateDirectoryConvention()
    {
        Assert.Equal("/var/lib/kgsm-monitor/events", JournalLayout.DirectoryFor("kgsm-monitor"));
    }

    [Fact]
    public void ProducerOf_ToleratesATrailingSeparator()
    {
        // A configured path is as likely to carry one as not, and a rule that answered "no producer"
        // for a perfectly conventional path would warn about correct configuration.
        Assert.Equal("kgsm-api", JournalLayout.ProducerOf("/var/lib/kgsm-api/events/"));
    }

    [Fact]
    public void ProducerOf_AcceptsARelocatedStateRoot()
    {
        // Only the two segments that carry meaning are read, so a test root or a host that keeps state
        // elsewhere is still conventional.
        Assert.Equal("kgsm-bot", JournalLayout.ProducerOf("/tmp/somewhere/kgsm-bot/events"));
    }

    [Theory]
    [InlineData("/var/lib/kgsm-api/journal")]      // not the journal subdirectory
    [InlineData("/var/lib/kgsm-api")]              // the state directory itself
    [InlineData("/var/lib/postgres/events")]       // outside this ecosystem, so outside the scan
    [InlineData("/var/lib/KGSM-Api/events")]       // not a usable producer id
    [InlineData("")]
    [InlineData(null)]
    public void ProducerOf_RefusesWhatAReaderWouldNotFind(string? directory)
    {
        Assert.Null(JournalLayout.ProducerOf(directory));
    }

    // ── Self-check: the writer asks what a reader will conclude ─────────────────────────

    [Fact]
    public void Mismatch_IsSilentForTheConvention()
    {
        Assert.Null(Options("kgsm-monitor", JournalLayout.DirectoryFor("kgsm-monitor")).DescribeDirectoryMismatch());
    }

    [Fact]
    public void Mismatch_IsSilentForARelocatedRoot()
    {
        // Test isolation and a host with state elsewhere are legitimate; neither changes the rule.
        Assert.Null(Options("kgsm-api", Path.Combine(_root, "kgsm-api", "events")).DescribeDirectoryMismatch());
    }

    [Fact]
    public void Mismatch_NamesTheProducerAReaderWouldAttributeInstead()
    {
        // The copy-paste that is one plausible mistake away: a leaf naming its producer after its unit
        // while writing to its state directory. Every event lands under the other name.
        string? problem = Options("kgsm-assistant-service", "/var/lib/kgsm-assistant/events")
            .DescribeDirectoryMismatch();

        Assert.NotNull(problem);
        Assert.Contains("kgsm-assistant", problem);
        Assert.Contains("kgsm-assistant-service", problem);
    }

    [Fact]
    public void Mismatch_SaysWhenNoReaderWouldFindItAtAll()
    {
        string? problem = Options("kgsm-api", "/opt/kgsm-api/audit").DescribeDirectoryMismatch();

        Assert.NotNull(problem);
        Assert.Contains("no producer", problem, StringComparison.OrdinalIgnoreCase);
    }

    // ── Version: one build identity, never a fabricated one ─────────────────────────────

    [Fact]
    public void Version_PrefersTheInformationalVersion()
    {
        // The number that matches what a repo publishes and a consumer pins, complete with whatever
        // source revision the build stamps on — where the assembly version is a four-part form no
        // released package is ever numbered with. This pairing is the drift the ecosystem had: three
        // producers stamping the right-hand value and one the left.
        Assert.Equal("2.5.2+3d0f4386", ProducerVersion.Resolve("2.5.2+3d0f4386", "2.5.2.0"));
    }

    [Fact]
    public void Version_FallsBackToTheAssemblyVersion()
    {
        Assert.Equal("1.5.1.0", ProducerVersion.Resolve(null, "1.5.1.0"));
        Assert.Equal("1.5.1.0", ProducerVersion.Resolve("  ", "1.5.1.0"));
    }

    [Fact]
    public void Version_IsNeverFabricated()
    {
        // Absent is the honest answer and the envelope already defines it — a reader sees no claim
        // about the build. A placeholder like 0.0.0 would name a build that was never made.
        Assert.Null(ProducerVersion.Resolve(null, null));
        Assert.Null(ProducerVersion.Resolve("", "  "));
    }

    [Fact]
    public void Version_ReadsARealAssembly()
    {
        // The wiring above the rule: an SDK-built assembly always carries an informational version, so
        // this is what every producer will actually stamp.
        Assembly assembly = typeof(JournalConformanceTests).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.False(string.IsNullOrWhiteSpace(informational));
        Assert.Equal(informational, ProducerVersion.Of(assembly));
    }

    // ── Identity: the actor is derived, not declared twice ──────────────────────────────

    [Theory]
    [InlineData("kgsm-watchdog", "system:watchdog")]
    [InlineData("kgsm-monitor", "system:monitor")]
    [InlineData("kgsm-firewall", "system:firewall")]
    [InlineData("kgsm", "system:kgsm")]
    public void SystemActor_IsDerivedFromTheProducer(string producer, string expected)
    {
        // The first two are the strings the watchdog and the monitor each held as their own constant.
        // Deriving them has to produce exactly those, or migrating a producer rewrites its history's
        // actor.
        Assert.Equal(expected, JournalProducer.SystemActorFor(producer));
    }

    // ── Presence: discoverable before it has anything to say ────────────────────────────

    [Fact]
    public void Registration_CreatesTheJournalDirectory()
    {
        // A consumer scans for producers when it starts. Left to the first event, a deployed producer
        // with nothing yet to record is invisible until it emits AND every consumer restarts.
        string directory = Path.Combine(_root, "kgsm-monitor", "events");
        Assert.False(Directory.Exists(directory));

        new ServiceCollection().AddKgsmJournal(
            "kgsm-monitor",
            typeof(JournalConformanceTests).Assembly,
            configure: o => o.Directory = directory);

        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void Registration_RelocatesEveryJournalUnderOneStateRoot()
    {
        // Deriving the path from the producer id means a component run by hand writes exactly where
        // the deployed one does — into this host's real audit record. There has to be one way to move
        // the whole layout aside, and it moves the root without bending the rule: same producer name,
        // same events subdirectory, somewhere else.
        new ServiceCollection().AddKgsmJournal(
            "kgsm-monitor", typeof(JournalConformanceTests).Assembly, stateRoot: _root);

        Assert.True(Directory.Exists(Path.Combine(_root, "kgsm-monitor", "events")));
    }

    [Fact]
    public void Registration_ReadsTheStateRootFromTheEnvironment()
    {
        string previous = Environment.GetEnvironmentVariable(
            JournalServiceCollectionExtensions.StateRootVariable) ?? string.Empty;

        try
        {
            Environment.SetEnvironmentVariable(
                JournalServiceCollectionExtensions.StateRootVariable, _root);

            new ServiceCollection().AddKgsmJournal(
                "kgsm-bot", typeof(JournalConformanceTests).Assembly);

            Assert.True(Directory.Exists(Path.Combine(_root, "kgsm-bot", "events")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                JournalServiceCollectionExtensions.StateRootVariable,
                previous.Length == 0 ? null : previous);
        }
    }

    [Fact]
    public void Registration_ResolvesOneWriterForTheProducer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddKgsmJournal(
            "kgsm-bot", typeof(JournalConformanceTests).Assembly,
            configure: o => o.Directory = Path.Combine(_root, "kgsm-bot", "events"));

        using ServiceProvider provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IEventJournalWriter>();

        Assert.Equal("kgsm-bot", writer.Producer);
        Assert.Same(writer, provider.GetRequiredService<IEventJournalWriter>());
    }

    [Fact]
    public void Writer_CreatesItsDirectoryWhenConstructedDirectly()
    {
        string directory = Path.Combine(_root, "kgsm-scheduler", "events");

        _ = new EventJournalWriter(
            Options("kgsm-scheduler", directory), new Mock<ILogger<EventJournalWriter>>().Object);

        Assert.True(Directory.Exists(directory));
    }

    // ── Resolution: which reader a consumer gets, in either call order ──────────────────

    [Theory]
    [InlineData(true)]   // AddKgsmServices first, then federation — the order that always worked
    [InlineData(false)]  // federation first — the order that silently did nothing
    public void Federation_WinsWhicheverOrderItWasRegisteredIn(bool servicesFirst)
    {
        // ⚠ The regression this exists for has no symptom. Two valid AddSingleton registrations of one
        // interface differ only in call order, so a consumer that federated too early kept reading its
        // single journal SUCCESSFULLY — healthy journal, quiet host, nothing to catch. Asserting that
        // resolution succeeded proves nothing; the assertion has to name the type.
        using ServiceProvider provider = BuildProvider(servicesFirst);

        Assert.IsType<FederatedEventSource>(provider.GetRequiredService<IEventSource>());
        Assert.IsType<FederatedEventJournalHistory>(provider.GetRequiredService<IEventJournalHistory>());
    }

    [Fact]
    public void WithoutFederation_TheSingleJournalReaderIsResolved()
    {
        // The other half of the rule: a consumer that never federates is unaffected by the resolution
        // seam existing at all.
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddKgsmServices(KgsmOptionsForTests());

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<EventJournalReader>(provider.GetRequiredService<IEventSource>());
        Assert.IsType<EventJournalHistory>(provider.GetRequiredService<IEventJournalHistory>());
    }

    [Fact]
    public void AConsumerCanStillSupplyItsOwnSource()
    {
        // Order-independence must not become "the library decides and you cannot". An explicit
        // registration after the fact still wins, the same way it does for IEventCursorStore.
        var own = new Mock<IEventSource>().Object;

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddKgsmServices(KgsmOptionsForTests());
        services.AddKgsmJournalFederation(engineJournalDirectory: _root, stateRoot: _root);
        services.AddSingleton(own);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(own, provider.GetRequiredService<IEventSource>());
    }

    [Fact]
    public void Discovery_ScansOnceHoweverManyReadersAskIt()
    {
        // The history reader and the live tail are built from this, and they have to see the same set
        // of producers: a journal appearing between two scans would leave one half of a consumer
        // permanently blind to a producer the other half reports on.
        Directory.CreateDirectory(Path.Combine(_root, "kgsm-watchdog", "events"));

        var discovery = new JournalDiscovery(
            Path.Combine(_root, "kgsm", "events"), _root, NullLogger<JournalDiscovery>.Instance);

        IReadOnlyList<JournalSource> first = discovery.Discover();

        // A producer that starts writing after the first scan is deliberately NOT picked up: the set is
        // fixed for the life of the process, so both readers keep agreeing.
        Directory.CreateDirectory(Path.Combine(_root, "kgsm-monitor", "events"));

        Assert.Same(first, discovery.Discover());
        Assert.DoesNotContain(discovery.Discover(), s => s.Producer == "kgsm-monitor");
    }

    // ── The write path ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_StampsTheDerivedActorAndSystemOrigin()
    {
        var recorder = new TestRecorder(Writer("kgsm-watchdog"));

        await recorder.RecordDefaultsAsync();

        JsonElement line = ReadOnlyLine("kgsm-watchdog");
        Assert.Equal("system:watchdog", line.GetProperty("Actor").GetString());
        Assert.Equal("system", line.GetProperty("Origin").GetString());
    }

    [Fact]
    public async Task Record_LetsTheCallSiteOverrideBoth()
    {
        // Carrying a caller's provenance is what a producer acting on somebody's behalf does; the
        // default is for a producer acting on its own.
        var recorder = new TestRecorder(Writer("kgsm-watchdog"));

        await recorder.RecordAsAsync("discord:someone", "ui");

        JsonElement line = ReadOnlyLine("kgsm-watchdog");
        Assert.Equal("discord:someone", line.GetProperty("Actor").GetString());
        Assert.Equal("ui", line.GetProperty("Origin").GetString());
    }

    [Fact]
    public async Task Record_OmitsAnActorAProducerCannotKnow()
    {
        // A producer whose events are driven by people overrides the default away. Attributing an
        // unknown actor to the daemon that carried the action out would state an author it has not got.
        var recorder = new TestRecorder(Writer("kgsm-api")) { SuppressDefaultActor = true };

        await recorder.RecordDefaultsAsync();

        JsonElement line = ReadOnlyLine("kgsm-api");
        Assert.False(line.TryGetProperty("Actor", out _));
    }

    [Fact]
    public async Task Record_NormalisesADashSeparatedType()
    {
        var recorder = new TestRecorder(Writer("kgsm-watchdog"));

        await recorder.RecordTypeAsync("instance-ready");

        Assert.Equal("instance_ready", ReadOnlyLine("kgsm-watchdog").GetProperty("EventType").GetString());
    }

    [Fact]
    public async Task Record_ReportsAFailedWriteWithoutThrowing()
    {
        // The action already happened. Refusing it because the record could not be written would trade
        // a missing line for broken behaviour — so the caller is told, and decides.
        var writer = new Mock<IEventJournalWriter>();
        writer.SetupGet(w => w.Producer).Returns("kgsm-monitor");
        writer
            .Setup(w => w.AppendAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk gone"));

        var recorder = new TestRecorder(writer.Object);

        Assert.False(await recorder.RecordDefaultsAsync());
    }

    [Fact]
    public void Record_WritesARealNullRatherThanAnEmptyString()
    {
        // An empty string is a third state the envelope does not define: neither a value nor the
        // absence of one, and a reader checking for null does not find it.
        var recorder = new TestRecorder(Writer("kgsm-monitor"));

        recorder.RecordNullablePayload(null);

        JsonElement data = ReadOnlyLine("kgsm-monitor").GetProperty("Data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("Reason").ValueKind);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>A container with both registrations, made in the order under test.</summary>
    private ServiceProvider BuildProvider(bool servicesFirst)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        if (servicesFirst)
        {
            services.AddKgsmServices(KgsmOptionsForTests());
            services.AddKgsmJournalFederation(engineJournalDirectory: _root, stateRoot: _root);
        }
        else
        {
            services.AddKgsmJournalFederation(engineJournalDirectory: _root, stateRoot: _root);
            services.AddKgsmServices(KgsmOptionsForTests());
        }

        return services.BuildServiceProvider();
    }

    /// <summary>Options pointed at this test's own root, so nothing reads the machine's journals.</summary>
    private KgsmOptions KgsmOptionsForTests() => new()
    {
        KgsmPath = Path.Combine(_root, "kgsm.sh"),
        EventJournalDirectory = Path.Combine(_root, "kgsm", "events"),
    };

    private EventJournalWriterOptions Options(string producer, string directory) => new()
    {
        Producer = producer,
        Directory = directory,
        Hostname = "testhost",
    };

    private EventJournalWriter Writer(string producer) => new(
        Options(producer, Path.Combine(_root, producer, "events")),
        new Mock<ILogger<EventJournalWriter>>().Object);

    private JsonElement ReadOnlyLine(string producer)
    {
        string directory = Path.Combine(_root, producer, "events");
        string[] segments = Directory.GetFiles(directory, "*.ndjson");
        string line = Assert.Single(File.ReadAllLines(Assert.Single(segments)));

        return JsonDocument.Parse(line).RootElement.Clone();
    }

    /// <summary>A producer, standing in for the four real ones so the shared half can be exercised.</summary>
    private sealed class TestRecorder(IEventJournalWriter writer)
        : JournalRecorder(writer, new Mock<ILogger>().Object)
    {
        public bool SuppressDefaultActor { get; init; }

        protected override string? DefaultActor => SuppressDefaultActor ? null : base.DefaultActor;

        public Task<bool> RecordDefaultsAsync() =>
            RecordAsync("thing_happened", w => w.WriteString("Subject", "x"));

        public Task<bool> RecordAsAsync(string actor, string origin) =>
            RecordAsync("thing_happened", w => w.WriteString("Subject", "x"), actor, origin);

        public Task<bool> RecordTypeAsync(string eventType) =>
            RecordAsync(eventType, w => w.WriteString("Subject", "x"));

        public bool RecordNullablePayload(string? reason) =>
            Record("thing_happened", w => WriteNullable(w, "Reason", reason));
    }
}
