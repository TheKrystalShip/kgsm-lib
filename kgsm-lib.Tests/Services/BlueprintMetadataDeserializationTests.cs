namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies the <c>kgsm blueprints info --json</c> wire shape — specifically the
/// new nested <c>Metadata</c> object — deserializes correctly through the real
/// <see cref="KgsmCommandExecutor"/> path (source-generated context + the global
/// string-coercion converters).
///
/// The load-bearing guarantee is KGSM's "never fabricate a metric" invariant: an
/// uncurated numeric metadata field arrives as JSON <c>null</c> and MUST stay
/// <c>int?</c> null — it must not be coerced to <c>0</c> by the global
/// <see cref="JsonStringToIntConverter"/>. A blueprint from an older KGSM with no
/// Metadata block at all must leave <see cref="Blueprint.Metadata"/> null.
/// </summary>
public class BlueprintMetadataDeserializationTests
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

    [Fact]
    public void BlueprintInfo_CuratedMetadata_DeserializesAllFields()
    {
        // A fully curated native blueprint. Top-level scalars arrive as strings
        // (KGSM convention) and the global converters coerce them.
        const string json = """
            {
              "Name": "7dtd",
              "Ports": [{"start":26900,"end":26903,"protocol":"tcp"},{"start":26900,"end":26903,"protocol":"udp"}],
              "BlueprintType": "Native",
              "SteamAppId": "294420",
              "ClientSteamAppId": "251570",
              "IsSteamAccountRequired": "false",
              "ExecutableFile": "7DaysToDieServer.x86_64",
              "LevelName": "default",
              "ExecutableSubdirectory": "/bin/x86_64",
              "ExecutableArguments": "-quit -batchmode",
              "StopCommand": "exit",
              "SaveCommand": "exit",
              "Metadata": {
                "DisplayName": "7 Days to Die",
                "Description": "Voxel survival horde game.",
                "MaxPlayers": 8,
                "MinRamMb": 4096,
                "RecommendedRamMb": 8192,
                "BaseDiskMb": 12000
              }
            }
            """;
        StubProcessOutput(json);

        Blueprint? bp = Create().ExecuteForJson<Blueprint>(["blueprints", "info", "7dtd", "--json"]);

        Assert.NotNull(bp);
        Assert.Equal("7dtd", bp!.Name);
        // String "false" coerced to bool via the global converter.
        Assert.False(bp.IsSteamAccountRequired);

        Assert.NotNull(bp.Metadata);
        Assert.Equal("7 Days to Die", bp.Metadata!.DisplayName);
        Assert.Equal("Voxel survival horde game.", bp.Metadata.Description);
        Assert.Equal(8, bp.Metadata.MaxPlayers);
        Assert.Equal(4096, bp.Metadata.MinRamMb);
        Assert.Equal(8192, bp.Metadata.RecommendedRamMb);
        Assert.Equal(12000, bp.Metadata.BaseDiskMb);
    }

    [Fact]
    public void BlueprintInfo_UncuratedMetadata_NumericNullsStayNull_NotZero()
    {
        // The invariant: nulls must NOT become 0. This is exactly the case the
        // global JsonStringToIntConverter would corrupt if a numeric field were
        // emitted as "" instead of null — so bash emits null, and int? short-
        // circuits the converter.
        const string json = """
            {
              "Name": "factorio",
              "Ports": [{"start":34197,"end":34197,"protocol":"tcp"},{"start":34197,"end":34197,"protocol":"udp"}],
              "BlueprintType": "Native",
              "SteamAppId": "0",
              "ClientSteamAppId": "0",
              "IsSteamAccountRequired": "false",
              "ExecutableFile": "factorio",
              "LevelName": "default",
              "ExecutableSubdirectory": "",
              "ExecutableArguments": "",
              "StopCommand": "",
              "SaveCommand": "",
              "Metadata": {
                "DisplayName": null,
                "Description": null,
                "MaxPlayers": null,
                "MinRamMb": null,
                "RecommendedRamMb": null,
                "BaseDiskMb": null
              }
            }
            """;
        StubProcessOutput(json);

        Blueprint? bp = Create().ExecuteForJson<Blueprint>(["blueprints", "info", "factorio", "--json"]);

        Assert.NotNull(bp);
        Assert.NotNull(bp!.Metadata);
        Assert.Null(bp.Metadata!.DisplayName);
        Assert.Null(bp.Metadata.Description);
        Assert.Null(bp.Metadata.MaxPlayers);
        Assert.Null(bp.Metadata.MinRamMb);
        Assert.Null(bp.Metadata.RecommendedRamMb);
        Assert.Null(bp.Metadata.BaseDiskMb);
    }

    [Fact]
    public void BlueprintInfo_NoMetadataBlock_MetadataIsNull()
    {
        // JSON from an older KGSM that predates the Metadata block. The library
        // must tolerate it: Metadata is simply null (additive, non-breaking).
        const string json = """
            {
              "Name": "valheim",
              "Ports": [{"start":2456,"end":2458,"protocol":"tcp"},{"start":2456,"end":2458,"protocol":"udp"}],
              "BlueprintType": "Native",
              "SteamAppId": "896660",
              "ClientSteamAppId": "892970",
              "IsSteamAccountRequired": "false",
              "ExecutableFile": "start_server_bepinex.sh",
              "LevelName": "default",
              "ExecutableSubdirectory": "",
              "ExecutableArguments": "-nographics",
              "StopCommand": "exit",
              "SaveCommand": ""
            }
            """;
        StubProcessOutput(json);

        Blueprint? bp = Create().ExecuteForJson<Blueprint>(["blueprints", "info", "valheim", "--json"]);

        Assert.NotNull(bp);
        Assert.Equal("valheim", bp!.Name);
        Assert.Null(bp.Metadata);
    }
}
