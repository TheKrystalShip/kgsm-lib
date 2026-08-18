namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies the backup-manifest wire shape (<c>instances backups &lt;instance&gt; --json</c>)
/// deserializes into <see cref="InstanceBackup"/>, and — the part that matters — that a manifest
/// written before it recorded a reason or a retention reads back honestly.
/// </summary>
/// <remarks>
/// Both documents below are the real thing: the second is copied from a backup that exists on a live
/// host, so what this asserts about missing fields is what the engine will actually hand a consumer
/// for every archive taken before those fields existed.
/// </remarks>
public class InstanceBackupManifestDeserializationTests
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

    private const string ListingJson = """
        [
          {
            "schema_version": 2,
            "id": "factorio-01-20260818T101500Z-a3f9c1",
            "instance": "factorio-01",
            "blueprint": "factorio",
            "version": "2.0.55",
            "created_at": "2026-08-18T10:15:00Z",
            "reason": "incident",
            "retention": "pinned",
            "compressed": true,
            "consistency": "hot",
            "sources": ["saves"],
            "size_bytes": 1048576,
            "file_count": 12,
            "sha256": "64201a8a452ca8f6deaa0f7681e70fa8e8689517b993851783cbc9653803d738"
          },
          {
            "schema_version": 2,
            "id": "factorio-01-20260818T090000Z-11bb22",
            "instance": "factorio-01",
            "blueprint": "factorio",
            "version": "2.0.54",
            "created_at": "2026-08-18T09:00:00Z",
            "reason": "pre-update",
            "retention": "prunable",
            "compressed": true,
            "consistency": "cold",
            "sources": ["install", "saves"],
            "size_bytes": 2097152,
            "file_count": 340,
            "sha256": null
          }
        ]
        """;

    // A manifest written before the two fields existed, as `backups --json` reports it: the engine
    // fills the retention in (its absence IS prunable) and leaves the reason null (nothing can
    // recover why it was taken).
    private const string LegacyListingJson = """
        [
          {
            "schema_version": 1,
            "id": "starbound-20260812T074638Z-67fb3b",
            "instance": "starbound",
            "blueprint": "starbound",
            "version": "16302742",
            "created_at": "2026-08-12T07:47:06Z",
            "reason": null,
            "retention": "prunable",
            "compressed": true,
            "consistency": "cold",
            "sources": ["install"],
            "size_bytes": 771023411,
            "file_count": 3835,
            "sha256": "64201a8a452ca8f6deaa0f7681e70fa8e8689517b993851783cbc9653803d738"
          }
        ]
        """;

    [Fact]
    public void Manifest_CarriesTheReasonAndTheRetention()
    {
        StubProcessOutput(ListingJson);

        List<InstanceBackup>? backups = Create()
            .ExecuteForJson<List<InstanceBackup>>(["instances", "backups", "factorio-01", "--json"]);

        Assert.NotNull(backups);
        Assert.Equal(2, backups!.Count);

        Assert.Equal(BackupReason.Incident, backups[0].Reason);
        Assert.Equal(BackupRetention.Pinned, backups[0].Retention);
        Assert.True(backups[0].IsPinned);

        // The rollback point an update left behind. Resolvable by reason — never by recency, which
        // at this moment would name the incident capture above it.
        Assert.Equal(BackupReason.PreUpdate, backups[1].Reason);
        Assert.False(backups[1].IsPinned);
    }

    [Fact]
    public void AManifestWithNoReason_ReadsAsUnknownAndPrunable()
    {
        StubProcessOutput(LegacyListingJson);

        List<InstanceBackup>? backups = Create()
            .ExecuteForJson<List<InstanceBackup>>(["instances", "backups", "starbound", "--json"]);

        Assert.NotNull(backups);
        InstanceBackup backup = Assert.Single(backups!);

        // Null, never a filled-in "scheduled": a classification nobody measured, read as one, is how
        // "restore the latest" becomes dangerous.
        Assert.Null(backup.Reason);
        // Absent retention is prunable — the behaviour this backup already had.
        Assert.False(backup.IsPinned);
        Assert.Equal(1, backup.SchemaVersion);
    }

    [Fact]
    public void AManifestWithNoRetentionKeyAtAll_IsNotPinned()
    {
        // Defence for a consumer reading a manifest file directly rather than through the engine's
        // listing, where the key really is absent.
        StubProcessOutput("""
            [{ "schema_version": 1, "id": "x-20260101T000000Z-aaaaaa", "created_at": "2026-01-01T00:00:00Z" }]
            """);

        List<InstanceBackup>? backups = Create()
            .ExecuteForJson<List<InstanceBackup>>(["instances", "backups", "x", "--json"]);

        InstanceBackup backup = Assert.Single(backups!);
        Assert.Null(backup.Retention);
        Assert.Null(backup.Reason);
        Assert.False(backup.IsPinned);
    }
}
