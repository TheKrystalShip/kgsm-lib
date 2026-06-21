namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies the real KGSM bulk-status wire shape
/// (<c>instances list --status --json</c>) deserializes correctly through
/// <see cref="KgsmCommandExecutor"/> into a keyed
/// <see cref="Reading{T}"/>-of-<see cref="InstanceRuntimeStatus"/> dictionary
/// (via <see cref="KgsmBulkStatusReadingConverter"/>).
///
/// The JSON below is captured verbatim from a live instance. It guards three
/// things that previously broke (or would break) the whole fleet read:
/// (1) <c>recent_logs</c> is a newline-joined <em>string</em>, not an array —
/// a single mistyped field throws in the one <c>Deserialize&lt;Dictionary&gt;</c>
/// call and collapses every instance to an empty result; (2) the tri-state
/// <c>version</c> block, where fast mode reports "unchecked" (nulls) instead of
/// fabricating an answer; (3) the failed-element shape KGSM emits for an instance
/// whose management file can't answer <c>--status</c> maps to a
/// <see cref="ReadingState.Unavailable"/> reading carrying the cause, instead of
/// a masquerading "stopped" status.
/// </summary>
public class InstanceStatusDeserializationTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<ILogger<KgsmCommandExecutor>> _logger = new();

    private KgsmCommandExecutor Create() =>
        new(_processRunner.Object,
            new KgsmOptions { KgsmPath = "/opt/kgsm/kgsm.sh", Timeouts = new KgsmTimeoutOptions() },
            _logger.Object);

    private void StubProcessOutput(string stdout) =>
        _processRunner
            .Setup(r => r.Execute(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Returns(new ProcessResult(0, stdout, string.Empty));

    // Captured verbatim from `./kgsm.sh instances list --status --json --fast`
    // against a live native instance. `\n` here is a JSON string escape (literal
    // backslash-n in source, via the raw string literal), exactly as on the wire.
    private const string LiveBulkFastJson = """
        {
          "7dtd": {
            "instance_name": "7dtd",
            "status": false,
            "process": { "pid": null, "status": null, "start_time": null },
            "version": { "current": "22422094", "latest": null, "checked": false, "updates_available": null },
            "configuration": {
              "blueprint": "7dtd.bp",
              "runtime": "native",
              "lifecycle_manager": "standalone",
              "directory": "/opt/7dtd/7dtd",
              "ports": "26900:26903/tcp|26900:26903/udp"
            },
            "resources": { "disk_usage": "16G" },
            "backups": [],
            "recent_logs": "      Peak Allocated memory 8.3 MB\nShutdown handler: cleanup.\n"
          }
        }
        """;

    [Fact]
    public void BulkFastStatus_DeserializesKeyedDictionary_WithStringRecentLogsAndTriStateVersion()
    {
        StubProcessOutput(LiveBulkFastJson);

        Dictionary<string, Reading<InstanceRuntimeStatus>>? result =
            Create().ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                ["instances", "list", "--status", "--json", "--fast"]);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("7dtd"));

        // a healthy element is a measured reading carrying the status value.
        Reading<InstanceRuntimeStatus> reading = result["7dtd"];
        Assert.Equal(ReadingState.Measured, reading.State);
        Assert.Null(reading.Code);
        InstanceRuntimeStatus s = reading.Value!;
        Assert.Equal("7dtd", s.InstanceName);
        Assert.False(s.Status);

        // recent_logs arrives as a string — the regression this test exists for.
        Assert.Contains("Peak Allocated memory", s.RecentLogs);

        // version is tri-state in fast mode: checked=false, nothing fabricated.
        Assert.Equal("22422094", s.Version.Current);
        Assert.False(s.Version.Checked);
        Assert.Null(s.Version.Latest);
        Assert.Null(s.Version.UpdatesAvailable);
    }

    [Fact]
    public void BulkStatus_NoLogInstance_RecentLogsEmptyArray_DeserializesToEmptyString()
    {
        // A fresh / never-logged instance: KGSM emits recent_logs as the array
        // [] (templates/manage.native.d/11-status.sh lines 98/101), not a string.
        // A plain-string model throws here and collapses the whole dict; the
        // converter normalizes [] to "".
        const string json = """
            {
              "fresh": {
                "instance_name": "fresh",
                "status": false,
                "process": { "pid": null, "status": null, "start_time": null },
                "version": { "current": "1", "latest": null, "checked": false, "updates_available": null },
                "configuration": { "blueprint": "x.bp", "runtime": "native", "lifecycle_manager": "standalone", "directory": "/x", "ports": "1/tcp" },
                "resources": { "disk_usage": "0" },
                "backups": [],
                "recent_logs": []
              }
            }
            """;
        StubProcessOutput(json);

        Dictionary<string, Reading<InstanceRuntimeStatus>>? result =
            Create().ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                ["instances", "list", "--status", "--json", "--fast"]);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(ReadingState.Measured, result["fresh"].State);
        Assert.Equal(string.Empty, result["fresh"].Value!.RecentLogs);
    }

    [Fact]
    public void BulkStatus_FailedElement_BecomesUnavailableReading_WithoutSinkingDictionary()
    {
        // Exact shape from commands/instances.sh `_get_instance_status_json`
        // for an instance whose management file predates `--status`.
        const string json = """
            {
              "ok": {
                "instance_name": "ok",
                "status": true,
                "process": { "pid": 1234, "status": "running", "start_time": null },
                "version": { "current": "1", "latest": null, "checked": false, "updates_available": null },
                "configuration": { "blueprint": "x.bp", "runtime": "native", "lifecycle_manager": "standalone", "directory": "/x", "ports": "1/tcp" },
                "resources": { "disk_usage": "1G" },
                "backups": [],
                "recent_logs": ""
              },
              "broken": { "error": "Management file does not support --status command", "instance": "broken", "requires_regeneration": true }
            }
            """;
        StubProcessOutput(json);

        Dictionary<string, Reading<InstanceRuntimeStatus>>? result =
            Create().ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                ["instances", "list", "--status", "--json"]);

        Assert.NotNull(result);
        // The bad element does not sink the good one.
        Assert.Equal(2, result!.Count);
        Assert.Equal(ReadingState.Measured, result["ok"].State);
        Assert.True(result["ok"].Value!.Status);

        // The failed element is a typed Unavailable reading, not a fake status.
        Reading<InstanceRuntimeStatus> broken = result["broken"];
        Assert.Equal(ReadingState.Unavailable, broken.State);
        Assert.Equal(ReadingCode.RequiresRegeneration, broken.Code);
        Assert.Equal("Management file does not support --status command", broken.Reason);
        Assert.Null(broken.Value); // no masquerading status object
    }
}

/// <summary>
/// Verifies the tolerant <c>start_time</c> converter
/// (<see cref="JsonTolerantUtcDateTimeConverter"/>) applied to
/// <see cref="ProcessInfo.StartTime"/>. The contract is honesty-critical: a value
/// only becomes non-null when it carries an explicit instant (UTC <c>Z</c> or an
/// offset), and it must bind to <see cref="DateTimeKind.Utc"/> (kgsm-api's
/// ServerAggregator drops a non-UTC kind). ANY other string — the old local-time
/// asctime form, the offset-less <c>2026-06-16 14:23:01</c>, empty, garbage — degrades
/// to <see langword="null"/> WITHOUT throwing, so a single bad value loses only its own
/// <c>start_time</c> instead of throwing inside the one bulk <c>Deserialize</c> call and
/// collapsing the whole roster (the regression this exists for). An offset-less value is
/// honest null, never a fabricated instant (no timezone is assumed).
///
/// Exercised through the real <see cref="KgsmCommandExecutor"/> bulk-status path so the
/// converter runs exactly as it does in production.
/// </summary>
public class StartTimeConverterTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<ILogger<KgsmCommandExecutor>> _logger = new();

    private KgsmCommandExecutor Create() =>
        new(_processRunner.Object,
            new KgsmOptions { KgsmPath = "/opt/kgsm/kgsm.sh", Timeouts = new KgsmTimeoutOptions() },
            _logger.Object);

    private DateTime? DeserializeStartTime(string startTimeJsonValue)
    {
        // A single-instance bulk-status blob with the start_time literal under test.
        string json = $$"""
            {
              "x": {
                "instance_name": "x",
                "status": true,
                "process": { "pid": 1234, "status": "running", "start_time": {{startTimeJsonValue}} },
                "version": { "current": "1", "latest": null, "checked": false, "updates_available": null },
                "configuration": { "blueprint": "x.bp", "runtime": "native", "directory": "/x" },
                "resources": { "disk_usage": "1G" },
                "backups": [],
                "recent_logs": ""
              }
            }
            """;
        _processRunner
            .Setup(r => r.Execute(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Returns(new ProcessResult(0, json, string.Empty));

        Dictionary<string, Reading<InstanceRuntimeStatus>>? result =
            Create().ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                ["instances", "list", "--status", "--json"]);

        Assert.NotNull(result);
        // The whole read must survive regardless of the start_time value — the point of
        // the converter. A bad value never sinks the dictionary.
        Reading<InstanceRuntimeStatus> reading = result![ "x" ];
        Assert.Equal(ReadingState.Measured, reading.State);
        return reading.Value!.Process.StartTime;
    }

    [Fact]
    public void IsoUtcZ_BindsToUtcKind()
    {
        // The form current KGSM emits. Must round-trip to a UTC-kind DateTime (the only
        // kind kgsm-api's ServerAggregator accepts).
        DateTime? dt = DeserializeStartTime("\"2026-06-21T23:53:46Z\"");
        Assert.NotNull(dt);
        Assert.Equal(DateTimeKind.Utc, dt!.Value.Kind);
        Assert.Equal(new DateTime(2026, 6, 21, 23, 53, 46, DateTimeKind.Utc), dt.Value);
    }

    [Fact]
    public void ExplicitOffset_NormalizedToUtcKind()
    {
        // An explicit offset pins a real instant; normalize to UTC (not null).
        DateTime? dt = DeserializeStartTime("\"2026-06-21T23:53:46+02:00\"");
        Assert.NotNull(dt);
        Assert.Equal(DateTimeKind.Utc, dt!.Value.Kind);
        Assert.Equal(new DateTime(2026, 6, 21, 21, 53, 46, DateTimeKind.Utc), dt.Value);
    }

    [Fact]
    public void JsonNull_BecomesNull()
    {
        Assert.Null(DeserializeStartTime("null"));
    }

    [Fact]
    public void OffsetlessSpaceFormat_BecomesNull_NeverFabricated()
    {
        // The load-bearing case: an offset-less string is ambiguous. A naive
        // DateTimeOffset.TryParse(...).UtcDateTime would ASSUME local time and fabricate a
        // wrong instant. The honest answer is null.
        Assert.Null(DeserializeStartTime("\"2026-06-16 14:23:01\""));
    }

    [Fact]
    public void OldAsctimeLocalFormat_BecomesNull()
    {
        // The legacy `date`/asctime form from un-regenerated instances — no offset, must
        // be null.
        Assert.Null(DeserializeStartTime("\"Sun Jun 21 23:53:46 2026\""));
    }

    [Fact]
    public void EmptyString_BecomesNull()
    {
        Assert.Null(DeserializeStartTime("\"\""));
    }

    [Fact]
    public void Garbage_BecomesNull_DoesNotThrow()
    {
        Assert.Null(DeserializeStartTime("\"not-a-date\""));
    }
}
