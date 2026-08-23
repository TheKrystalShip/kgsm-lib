namespace TheKrystalShip.KGSM.Tests.Services;

using TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Verifies the real KGSM wire shape for <c>libraries list --json</c> deserializes into
/// <see cref="Library"/>.
///
/// The online entry is captured verbatim from a live host. The offline entry carries the
/// thing this shape exists to express: an unreachable root reports <see langword="null"/>
/// capacity, because nothing measured it — while its instance count is still answered, being
/// read from the instance registry rather than from the disk.
/// </summary>
public class LibraryDeserializationTests
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

    private const string LiveLibrariesJson = """
        [
          {
            "name": "default",
            "path": "/opt",
            "state": "online",
            "free_bytes": 634046873600,
            "total_bytes": 982820896768,
            "instance_count": 7
          },
          {
            "name": "archive",
            "path": "/mnt/archive",
            "state": "offline",
            "free_bytes": null,
            "total_bytes": null,
            "instance_count": 2
          }
        ]
        """;

    [Fact]
    public void OnlineLibrary_CarriesCapacityBeyondAnInt()
    {
        StubProcessOutput(LiveLibrariesJson);

        var libraries = Create().ExecuteForJson<List<Library>>(["libraries", "list", "--json"]);

        Assert.NotNull(libraries);
        Assert.Equal(2, libraries!.Count);

        Library online = libraries[0];
        Assert.Equal("default", online.Name);
        Assert.Equal("/opt", online.Path);
        Assert.Equal(LibraryState.Online, online.State);
        Assert.True(online.Online);
        Assert.Equal(634046873600L, online.FreeBytes);
        Assert.Equal(982820896768L, online.TotalBytes);
        Assert.Equal(7, online.InstanceCount);
    }

    [Fact]
    public void OfflineLibrary_ReportsNullCapacityAndStillCountsItsInstances()
    {
        StubProcessOutput(LiveLibrariesJson);

        var libraries = Create().ExecuteForJson<List<Library>>(["libraries", "list", "--json"]);

        Library offline = libraries![1];
        Assert.Equal(LibraryState.Offline, offline.State);
        Assert.False(offline.Online);
        Assert.Null(offline.FreeBytes);
        Assert.Null(offline.TotalBytes);
        Assert.Equal(2, offline.InstanceCount);
    }

    [Fact]
    public void NoLibrariesRegistered_DeserializesToAnEmptyList()
    {
        StubProcessOutput("[]");

        var libraries = Create().ExecuteForJson<List<Library>>(["libraries", "list", "--json"]);

        Assert.NotNull(libraries);
        Assert.Empty(libraries!);
    }

    [Fact]
    public void Instance_CarriesItsLibraryRootAndResolvedName()
    {
        // Trimmed from `kgsm instances info minecraft --json` on a live host. install_dir is the
        // game-binaries subdirectory of working_dir; library_dir is the root the instance sits under.
        StubProcessOutput("""
            {
              "name": "minecraft",
              "working_dir": "/opt/minecraft/minecraft",
              "install_dir": "/opt/minecraft/minecraft/install",
              "library_dir": "/opt",
              "library": "default"
            }
            """);

        var instance = Create().ExecuteForJson<Instance>(["instances", "info", "minecraft", "--json"]);

        Assert.NotNull(instance);
        Assert.Equal("/opt", instance!.LibraryDir);
        Assert.Equal("default", instance.Library);
        Assert.Equal("/opt/minecraft/minecraft/install", instance.InstallDir);
    }

    [Fact]
    public void Instance_OutsideAnyRegisteredLibrary_ReportsUnregisteredRatherThanGuessing()
    {
        StubProcessOutput("""
            {
              "name": "stray",
              "working_dir": "/srv/games/stray",
              "library_dir": "/srv/games",
              "library": "unregistered"
            }
            """);

        var instance = Create().ExecuteForJson<Instance>(["instances", "info", "stray", "--json"]);

        Assert.Equal("unregistered", instance!.Library);
    }
}
