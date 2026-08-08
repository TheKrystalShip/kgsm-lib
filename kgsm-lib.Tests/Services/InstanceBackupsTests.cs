using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for <see cref="InstanceBackups"/> — the jailed read authority for an instance's backups store.
/// Like <see cref="InstanceFilesTests"/> this is a security boundary, so it runs against a REAL temp-dir
/// jail rather than mocked <c>System.IO</c>.
/// </summary>
public sealed class InstanceBackupsTests : IDisposable
{
    private const string InstanceName = "test-instance";

    private readonly string _tempBase;
    private readonly string _root;
    private readonly Mock<IInstanceService> _mockInstances;
    private readonly InstanceBackups _sut;

    public InstanceBackupsTests()
    {
        // root = <tempBase>/bk — a known, non-random leaf so the sibling-prefix test can plant a real
        // "<tempBase>/bk-evil" neighbour, which a naive StartsWith(root) check would wrongly admit.
        _tempBase = Directory.CreateTempSubdirectory("kgsm-instancebackups-").FullName;
        _root = Path.Combine(_tempBase, "bk");
        Directory.CreateDirectory(_root);

        _mockInstances = new Mock<IInstanceService>();
        _mockInstances.Setup(x => x.GetInstanceInfo(InstanceName))
            .Returns(new Instance { Name = InstanceName, BackupsDir = _root });

        _sut = new InstanceBackups(_mockInstances.Object, NullLogger<InstanceBackups>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempBase, recursive: true); } catch { /* best-effort cleanup */ }
    }

    /// <summary>Creates a backup directory carrying a manifest and, optionally, an archive.</summary>
    private string MakeBackup(string id, bool compressed, string? sha256 = "abc123",
        byte[]? archiveBytes = null, bool writeManifest = true)
    {
        string dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);

        if (writeManifest)
        {
            string shaJson = sha256 is null ? "null" : $"\"{sha256}\"";
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                $$"""
                {"schema_version":1,"id":"{{id}}","compressed":{{(compressed ? "true" : "false")}},"sha256":{{shaJson}}}
                """);
        }

        if (archiveBytes is not null)
            File.WriteAllBytes(Path.Combine(dir, "data.tar.gz"), archiveBytes);

        return dir;
    }

    [Fact]
    public void OpenArchive_CompressedBackup_ReturnsTheBytesAndTheManifestDigest()
    {
        byte[] payload = Encoding.UTF8.GetBytes("not really a tarball, but bytes are bytes");
        MakeBackup("inst-20260808T120000Z-aaaaaa", compressed: true, sha256: "deadbeef", archiveBytes: payload);

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "inst-20260808T120000Z-aaaaaa");

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        using BackupArchive archive = result.Value!;
        Assert.Equal("data.tar.gz", archive.FileName);
        Assert.Equal(payload.Length, archive.SizeBytes);
        // The digest is the manifest's, carried through verbatim — never recomputed from the bytes we
        // are about to serve, which would prove nothing about their provenance.
        Assert.Equal("deadbeef", archive.Sha256);

        using var ms = new MemoryStream();
        archive.Content.CopyTo(ms);
        Assert.Equal(payload, ms.ToArray());
    }

    [Fact]
    public void OpenArchive_UncompressedBackup_IsRefused()
    {
        // The whole point of the compressed-only contract: a data/ tree is not one artifact, and there
        // is no digest to verify it with. Refused rather than tarred into something the manifest never
        // described.
        string dir = MakeBackup("inst-20260808T120000Z-bbbbbb", compressed: false, sha256: null);
        Directory.CreateDirectory(Path.Combine(dir, "data"));

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "inst-20260808T120000Z-bbbbbb");

        Assert.Equal(FileOpOutcome.NotAFile, result.Outcome);
    }

    [Fact]
    public void OpenArchive_NoManifest_IsNotABackup()
    {
        // A directory still being staged, or a foreign one someone dropped in the store. The manifest is
        // the gate — the same rule the engine's own listing applies, so the two cannot disagree.
        Directory.CreateDirectory(Path.Combine(_root, "half-built"));
        File.WriteAllBytes(Path.Combine(_root, "half-built", "data.tar.gz"), [1, 2, 3]);

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "half-built");

        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void OpenArchive_UnreadableManifest_IsNotABackup()
    {
        string dir = Path.Combine(_root, "corrupt");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"), "{ this is not json");
        File.WriteAllBytes(Path.Combine(dir, "data.tar.gz"), [1, 2, 3]);

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "corrupt");

        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void OpenArchive_CompressedButArchiveMissing_IsNotFound()
    {
        MakeBackup("inst-20260808T120000Z-cccccc", compressed: true, archiveBytes: null);

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "inst-20260808T120000Z-cccccc");

        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void OpenArchive_UnknownBackup_IsNotFound()
    {
        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "no-such-backup");

        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
    }

    // --- the jail ------------------------------------------------------------------------------------
    [Theory]
    [InlineData("../escape")]
    [InlineData("../../etc")]
    [InlineData("sub/nested")]
    [InlineData("/etc")]
    [InlineData(".")]
    [InlineData("..")]
    public void OpenArchive_PathLikeId_IsRefusedAsOutOfJail(string id)
    {
        // A backup id is a single directory name, never a path. A separator is refused for what it is
        // rather than relying on the jail to catch where it lands.
        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, id);

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
    }

    [Fact]
    public void OpenArchive_SymlinkPointingOutOfTheStore_IsRefused()
    {
        // A backup-shaped directory OUTSIDE the store, symlinked in under a legitimate-looking id. The
        // jail must resolve the link and refuse, since the real path is not a descendant of the root.
        string outside = Path.Combine(_tempBase, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "manifest.json"), """{"compressed":true,"sha256":"x"}""");
        File.WriteAllBytes(Path.Combine(outside, "data.tar.gz"), [9, 9, 9]);

        Directory.CreateSymbolicLink(Path.Combine(_root, "inst-20260808T120000Z-dddddd"), outside);

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "inst-20260808T120000Z-dddddd");

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
    }

    [Fact]
    public void OpenArchive_SiblingDirectorySharingTheRootPrefix_IsRefused()
    {
        // "<tempBase>/bk-evil" shares the "<tempBase>/bk" prefix but is NOT inside it. A containment
        // check missing the trailing separator would admit it.
        string sibling = _root + "-evil";
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "manifest.json"), """{"compressed":true,"sha256":"x"}""");
        File.WriteAllBytes(Path.Combine(sibling, "data.tar.gz"), [7]);

        Directory.CreateSymbolicLink(Path.Combine(_root, "sneaky"), sibling);

        FileOpResult<BackupArchive> result = _sut.OpenArchive(InstanceName, "sneaky");

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
    }

    [Fact]
    public void OpenArchive_InstanceWithNoBackupsDir_IsUnavailable()
    {
        _mockInstances.Setup(x => x.GetInstanceInfo("no-backups-dir"))
            .Returns(new Instance { Name = "no-backups-dir", BackupsDir = "" });

        FileOpResult<BackupArchive> result = _sut.OpenArchive("no-backups-dir", "anything");

        // Distinct from NotFound: we could not resolve WHERE to look, which is not the same statement
        // as having looked and found nothing.
        Assert.Equal(FileOpOutcome.InstanceUnavailable, result.Outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void OpenArchive_BlankArguments_Throw(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => _sut.OpenArchive(InstanceName, value!));
        Assert.ThrowsAny<ArgumentException>(() => _sut.OpenArchive(value!, "some-backup"));
    }
}
