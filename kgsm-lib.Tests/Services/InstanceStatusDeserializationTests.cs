namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies the real KGSM bulk-status wire shape
/// (<c>instances list --status --json</c>) deserializes correctly through
/// <see cref="KgsmCommandExecutor"/> into a keyed
/// <see cref="InstanceRuntimeStatus"/> dictionary.
///
/// The JSON below is captured verbatim from a live instance. It guards two
/// things that previously broke (or would break) the whole fleet read:
/// (1) <c>recent_logs</c> is a newline-joined <em>string</em>, not an array —
/// a single mistyped field throws in the one <c>Deserialize&lt;Dictionary&gt;</c>
/// call and collapses every instance to an empty result; (2) the tri-state
/// <c>version</c> block, where fast mode reports "unchecked" (nulls) instead of
/// fabricating an answer. A third test pins the failed-element shape KGSM emits
/// for an instance whose management file can't answer <c>--status</c>.
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

        Dictionary<string, InstanceRuntimeStatus>? result =
            Create().ExecuteForJson<Dictionary<string, InstanceRuntimeStatus>>(
                ["instances", "list", "--status", "--json", "--fast"]);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("7dtd"));

        InstanceRuntimeStatus s = result["7dtd"];
        Assert.Equal("7dtd", s.InstanceName);
        Assert.False(s.Status);

        // recent_logs arrives as a string — the regression this test exists for.
        Assert.Contains("Peak Allocated memory", s.RecentLogs);

        // version is tri-state in fast mode: checked=false, nothing fabricated.
        Assert.Equal("22422094", s.Version.Current);
        Assert.False(s.Version.Checked);
        Assert.Null(s.Version.Latest);
        Assert.Null(s.Version.UpdatesAvailable);

        // a healthy element carries no error flags.
        Assert.Null(s.Error);
        Assert.Null(s.RequiresRegeneration);
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

        Dictionary<string, InstanceRuntimeStatus>? result =
            Create().ExecuteForJson<Dictionary<string, InstanceRuntimeStatus>>(
                ["instances", "list", "--status", "--json", "--fast"]);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(string.Empty, result["fresh"].RecentLogs);
    }

    [Fact]
    public void BulkStatus_FailedElement_DeserializesWithErrorFlags_WithoutSinkingDictionary()
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

        Dictionary<string, InstanceRuntimeStatus>? result =
            Create().ExecuteForJson<Dictionary<string, InstanceRuntimeStatus>>(
                ["instances", "list", "--status", "--json"]);

        Assert.NotNull(result);
        // The bad element does not sink the good one.
        Assert.Equal(2, result!.Count);
        Assert.True(result["ok"].Status);
        Assert.Null(result["ok"].Error);

        InstanceRuntimeStatus broken = result["broken"];
        Assert.Equal("Management file does not support --status command", broken.Error);
        Assert.True(broken.RequiresRegeneration);
        Assert.False(broken.Status); // degenerate but safe — detectable via Error
    }
}
