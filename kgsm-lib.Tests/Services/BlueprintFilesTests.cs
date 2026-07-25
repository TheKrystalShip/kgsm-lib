using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for <see cref="BlueprintFiles"/> — the write-side authority for native-runtime blueprint files.
/// Like <see cref="InstanceFilesTests"/>, this runs against a REAL temp-dir jail rather than mocked
/// <c>System.IO</c> (the jail IS the security boundary under test); only the engine query
/// (<c>kgsm --paths --json</c>, via <see cref="IKgsmCommandExecutor"/>) is mocked.
/// </summary>
public sealed class BlueprintFilesTests : IDisposable
{
    private readonly string _userDir;
    private readonly Mock<IKgsmCommandExecutor> _mockExecutor;
    private readonly BlueprintFiles _sut;

    public BlueprintFilesTests()
    {
        _userDir = Directory.CreateTempSubdirectory("kgsm-blueprintfiles-user-").FullName;
        _mockExecutor = new Mock<IKgsmCommandExecutor>();
        SetPathsOutput(_userDir);

        _sut = new BlueprintFiles(_mockExecutor.Object, NullLogger<BlueprintFiles>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_userDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    /// <summary>Mocks the deserialized <c>kgsm --paths --json</c> result the way the real CLI emits it
    /// (see <c>kgsm.sh</c>'s <c>_cmd_paths</c>) — only <c>user.KGSM_USER_BLUEPRINTS_DIR</c> is load-bearing
    /// for the resolver under test; the rest is realistic filler. Pass a null dir to model a JSON payload
    /// that omits the user blueprints directory.</summary>
    private void SetPathsOutput(string? userBlueprintsDir)
    {
        var paths = new KgsmPaths
        {
            System = new KgsmSystemPaths { Root = "/opt/kgsm", SystemBlueprintsDir = "/opt/kgsm/blueprints" },
            User = new KgsmUserPaths
            {
                DataDir = "/home/x/.local/share/kgsm",
                UserBlueprintsDir = userBlueprintsDir,
                UserOverridesDir = "/home/x/.local/share/kgsm/overrides",
            },
        };
        SetPathsResult(paths);
    }

    /// <summary>Low-level setup for the mocked <c>kgsm --paths --json</c> deserialization — a
    /// <see langword="null"/> models any engine/parse failure (incl. an engine too old for --json).</summary>
    private void SetPathsResult(KgsmPaths? paths)
    {
        _mockExecutor
            .Setup(x => x.ExecuteForJson<KgsmPaths>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<KgsmPaths?>()))
            .Returns(paths);
    }

    private string Path_(string name) => Path.Combine(_userDir, name + ".bp.yaml");

    private static NativeBlueprintDraft MinimalDraft(string name, string executableFile = "run.sh") =>
        new()
        {
            Name = name,
            Native = new NativeBlueprintNativeDraft { ExecutableFile = executableFile },
        };

    // ---- constructor guards ------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlueprintFiles(null!, NullLogger<BlueprintFiles>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlueprintFiles(_mockExecutor.Object, null!));
    }

    // ---- argument validation -------------------------------------------------------------------------

    [Fact]
    public void Create_NullDraft_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.Create(null!));
    }

    [Fact]
    public void Remove_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.Remove(null!));
    }

    [Fact]
    public void Remove_WhitespaceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.Remove("   "));
    }

    [Fact]
    public void Exists_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.Exists(null!));
    }

    // ---- structural validation (guardrail #1: structural only, never semantic) -----------------------

    [Fact]
    public void Create_BlankExecutableFile_ReturnsInvalidDraft_AndWritesNothing()
    {
        var draft = new NativeBlueprintDraft
        {
            Name = "noexe",
            Native = new NativeBlueprintNativeDraft { ExecutableFile = "" },
        };

        var result = _sut.Create(draft);

        Assert.Equal(FileOpOutcome.InvalidDraft, result.Outcome);
        Assert.False(File.Exists(Path_("noexe")));
    }

    [Fact]
    public void Create_WhitespaceExecutableFile_ReturnsInvalidDraft()
    {
        var draft = new NativeBlueprintDraft
        {
            Name = "noexe2",
            Native = new NativeBlueprintNativeDraft { ExecutableFile = "   " },
        };

        var result = _sut.Create(draft);

        Assert.Equal(FileOpOutcome.InvalidDraft, result.Outcome);
    }

    // ---- name safety (rejected BEFORE any disk access) ------------------------------------------------

    [Theory]
    [InlineData("../evil")]
    [InlineData("/etc/x")]
    [InlineData("Foo Bar")]
    [InlineData("")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("has/slash")]
    [InlineData("has..dots")]
    public void Create_UnsafeName_ReturnsOutOfJail_AndWritesNothing(string badName)
    {
        var result = _sut.Create(MinimalDraft(badName));

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_userDir));
    }

    [Fact]
    public void Create_NameOverMaxLength_ReturnsOutOfJail()
    {
        string tooLong = new string('a', 65);

        var result = _sut.Create(MinimalDraft(tooLong));

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("/etc/x")]
    [InlineData("Foo Bar")]
    public void Remove_UnsafeName_ReturnsOutOfJail(string badName)
    {
        var result = _sut.Remove(badName);
        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("Foo Bar")]
    public void Exists_UnsafeName_ReturnsOutOfJail(string badName)
    {
        var result = _sut.Exists(badName);
        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
    }

    // ---- user-blueprints-dir resolution (learned from the engine) ------------------------------------

    [Fact]
    public void Create_PathsCommandFails_ReturnsBlueprintsDirUnavailable()
    {
        // Null models a failed exec / JSON parse failure / an engine too old to know --json.
        SetPathsResult(null);

        var result = _sut.Create(MinimalDraft("anything"));

        Assert.Equal(FileOpOutcome.BlueprintsDirUnavailable, result.Outcome);
    }

    [Fact]
    public void Create_PathsJsonOmitsUserBlueprintsDir_ReturnsBlueprintsDirUnavailable()
    {
        SetPathsResult(new KgsmPaths { System = new KgsmSystemPaths { Root = "/opt/kgsm" }, User = null });

        var result = _sut.Create(MinimalDraft("anything"));

        Assert.Equal(FileOpOutcome.BlueprintsDirUnavailable, result.Outcome);
    }

    [Fact]
    public void Create_PathsCommandThrows_ReturnsBlueprintsDirUnavailable()
    {
        _mockExecutor
            .Setup(x => x.ExecuteForJson<KgsmPaths>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<KgsmPaths?>()))
            .Throws(new InvalidOperationException("no kgsm on PATH"));

        var result = _sut.Create(MinimalDraft("anything"));

        Assert.Equal(FileOpOutcome.BlueprintsDirUnavailable, result.Outcome);
    }

    [Fact]
    public void Create_ReportedDirDoesNotExistOnDisk_ReturnsBlueprintsDirUnavailable()
    {
        string missing = Path.Combine(_userDir, "does-not-exist-subdir");
        SetPathsOutput(missing);

        var result = _sut.Create(MinimalDraft("anything"));

        Assert.Equal(FileOpOutcome.BlueprintsDirUnavailable, result.Outcome);
    }

    // ---- create: happy path + collision policy ---------------------------------------------------

    [Fact]
    public void Create_NewBlueprint_WritesFileIntoUserDir()
    {
        var result = _sut.Create(MinimalDraft("newgame"));

        Assert.True(result.IsOk);
        Assert.True(File.Exists(Path_("newgame")));
        Assert.StartsWith("sha256:", result.Value!.Etag);
        Assert.Equal(new FileInfo(Path_("newgame")).Length, result.Value.SizeBytes);
        // no leftover temp files from the atomic rename
        Assert.DoesNotContain(Directory.EnumerateFiles(_userDir), f => Path.GetFileName(f).Contains(".tmp-"));
    }

    [Fact]
    public void Create_NameCollision_OverwriteFalse_ReturnsAlreadyExists_AndDoesNotModifyFile()
    {
        _sut.Create(MinimalDraft("dup", "first.sh"));
        string originalContent = File.ReadAllText(Path_("dup"));

        var result = _sut.Create(MinimalDraft("dup", "second.sh"));

        Assert.Equal(FileOpOutcome.AlreadyExists, result.Outcome);
        Assert.Equal(originalContent, File.ReadAllText(Path_("dup")));
    }

    [Fact]
    public void Create_NameCollision_OverwriteTrue_Replaces()
    {
        _sut.Create(MinimalDraft("dup2", "first.sh"));

        var result = _sut.Create(MinimalDraft("dup2", "second.sh"), overwrite: true);

        Assert.True(result.IsOk);
        Assert.Contains("second.sh", File.ReadAllText(Path_("dup2")));
    }

    // ---- exists ------------------------------------------------------------------------------------

    [Fact]
    public void Exists_AfterCreate_ReturnsTrue()
    {
        _sut.Create(MinimalDraft("existcheck"));

        var result = _sut.Exists("existcheck");

        Assert.True(result.IsOk);
        Assert.True(result.Value);
    }

    [Fact]
    public void Exists_NeverCreated_ReturnsFalse()
    {
        var result = _sut.Exists("neverexisted");

        Assert.True(result.IsOk);
        Assert.False(result.Value);
    }

    // ---- remove --------------------------------------------------------------------------------------

    [Fact]
    public void Remove_ExistingUserBlueprint_Deletes()
    {
        _sut.Create(MinimalDraft("removeme"));

        var result = _sut.Remove("removeme");

        Assert.True(result.IsOk);
        Assert.False(File.Exists(Path_("removeme")));
    }

    [Fact]
    public void Remove_NeverExisted_ReturnsNotFound()
    {
        var result = _sut.Remove("ghost");
        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void Remove_NameOnlyPresentInADifferentDirectory_NeverTouchesIt_ReturnsNotFound()
    {
        // Simulates a same-named SYSTEM blueprint living in a completely different directory
        // (KGSM_SYSTEM_BLUEPRINTS_DIR) — Remove's target path is always <userDir>/<name>.bp.yaml, so it
        // structurally cannot resolve into this other directory regardless of what's in it.
        string systemLikeDir = Directory.CreateTempSubdirectory("kgsm-blueprintfiles-system-").FullName;
        try
        {
            string decoy = Path.Combine(systemLikeDir, "shared.bp.yaml");
            File.WriteAllText(decoy, "schema_version: 1\nname: shared\n");

            var result = _sut.Remove("shared");

            Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
            Assert.True(File.Exists(decoy)); // untouched
        }
        finally { try { Directory.Delete(systemLikeDir, recursive: true); } catch { } }
    }

    // ---- YAML templating: golden output (guardrail #3 — deterministic string, no YAML library) -------

    [Fact]
    public void Create_FullyPopulatedDraft_RendersExpectedYaml()
    {
        var draft = new NativeBlueprintDraft
        {
            Name = "mygame",
            PlayerJoinedRegex = "Got char (?<name>.+)",
            PlayerLeftRegex = "Lost char (?<name>.+)",
            Metadata = new NativeBlueprintMetadataDraft
            {
                DisplayName = "My Game",
                Description = "A test game.",
                RawgSlug = "my-game",
                MaxPlayers = 10,
                MinRamMb = 512,
                RecommendedRamMb = 1024,
                BaseDiskMb = 2048,
            },
            Native = new NativeBlueprintNativeDraft
            {
                Ports = "1234:1234/tcp",
                SteamAppId = 111,
                ClientSteamAppId = 222,
                SteamcmdArguments = "+beta test",
                IsSteamAccountRequired = true,
                Platform = "linux",
                LevelName = "world1",
                ExecutableSubdirectory = "bin/x64",
                ExecutableFile = "start.sh",
                ExecutableArguments = "--port 1234 $instance_name",
                StopCommand = "quit",
                SaveCommand = "save",
                StartupSuccessRegex = "Server started",
            },
        };

        var result = _sut.Create(draft);
        Assert.True(result.IsOk);

        const string expected =
            "schema_version: 1\n" +
            "name: mygame\n" +
            "runtime: native\n" +
            "metadata:\n" +
            "  display_name: 'My Game'\n" +
            "  description: 'A test game.'\n" +
            "  rawg_slug: 'my-game'\n" +
            "  max_players: 10\n" +
            "  min_ram_mb: 512\n" +
            "  recommended_ram_mb: 1024\n" +
            "  base_disk_mb: 2048\n" +
            "player_joined_regex: 'Got char (?<name>.+)'\n" +
            "player_left_regex: 'Lost char (?<name>.+)'\n" +
            "native:\n" +
            "  ports: '1234:1234/tcp'\n" +
            "  steam_app_id: 111\n" +
            "  client_steam_app_id: 222\n" +
            "  steamcmd_arguments: '+beta test'\n" +
            "  is_steam_account_required: true\n" +
            "  platform: 'linux'\n" +
            "  level_name: 'world1'\n" +
            "  executable_subdirectory: 'bin/x64'\n" +
            "  executable_file: 'start.sh'\n" +
            "  executable_arguments: '--port 1234 $instance_name'\n" +
            "  stop_command: 'quit'\n" +
            "  save_command: 'save'\n" +
            "  startup_success_regex: 'Server started'\n";

        Assert.Equal(expected, File.ReadAllText(Path_("mygame")));
    }

    [Fact]
    public void Create_MinimalDraft_RendersNullMetadata_NeverFabricatedZero()
    {
        var draft = new NativeBlueprintDraft
        {
            Name = "minimal",
            Native = new NativeBlueprintNativeDraft { ExecutableFile = "run.sh" },
        };

        var result = _sut.Create(draft);
        Assert.True(result.IsOk);

        const string expected =
            "schema_version: 1\n" +
            "name: minimal\n" +
            "runtime: native\n" +
            "metadata:\n" +
            "  display_name: null\n" +
            "  description: null\n" +
            "  rawg_slug: null\n" +
            "  max_players: null\n" +
            "  min_ram_mb: null\n" +
            "  recommended_ram_mb: null\n" +
            "  base_disk_mb: null\n" +
            "native:\n" +
            "  ports: ''\n" +
            "  steam_app_id: 0\n" +
            "  client_steam_app_id: 0\n" +
            "  steamcmd_arguments: ''\n" +
            "  is_steam_account_required: false\n" +
            "  platform: 'linux'\n" +
            "  level_name: 'default'\n" +
            "  executable_subdirectory: ''\n" +
            "  executable_file: 'run.sh'\n" +
            "  executable_arguments: ''\n" +
            "  stop_command: ''\n" +
            "  save_command: ''\n" +
            "  startup_success_regex: ''\n";

        string actual = File.ReadAllText(Path_("minimal"));
        Assert.Equal(expected, actual);
        Assert.DoesNotContain("max_players: 0", actual); // the no-fabricate invariant, spelled out
    }

    [Fact]
    public void Create_ValueContainingSingleQuote_EscapesByDoubling()
    {
        var draft = new NativeBlueprintDraft
        {
            Name = "quotetest",
            Native = new NativeBlueprintNativeDraft
            {
                ExecutableFile = "run.sh",
                ExecutableArguments = "it's a test",
            },
        };

        var result = _sut.Create(draft);

        Assert.True(result.IsOk);
        Assert.Contains("executable_arguments: 'it''s a test'", File.ReadAllText(Path_("quotetest")));
    }

    // ---- Render / TryParse (the editable-review round-trip) ---------------------------------------

    [Fact]
    public void Render_MatchesWhatCreateWrites_WithoutTouchingDisk()
    {
        var draft = FullDraft();

        // Render is the exact text Create would persist — but pure (no engine call, no file).
        _sut.Create(draft);
        Assert.Equal(File.ReadAllText(Path_("roundtrip")), _sut.Render(draft));
    }

    [Fact]
    public void RenderThenParse_RoundTripsEveryField()
    {
        var draft = FullDraft();

        var parsed = _sut.TryParse(_sut.Render(draft));

        Assert.True(parsed.IsOk);
        var d = parsed.Value!;
        Assert.Equal(draft.Name, d.Name);
        Assert.Equal(draft.Metadata.DisplayName, d.Metadata.DisplayName);
        Assert.Equal(draft.Metadata.MaxPlayers, d.Metadata.MaxPlayers);
        Assert.Equal(draft.Metadata.BaseDiskMb, d.Metadata.BaseDiskMb);
        Assert.Equal(draft.Native.Ports, d.Native.Ports);
        Assert.Equal(draft.Native.SteamAppId, d.Native.SteamAppId);
        Assert.Equal(draft.Native.ClientSteamAppId, d.Native.ClientSteamAppId);
        Assert.Equal(draft.Native.ExecutableFile, d.Native.ExecutableFile);
        Assert.Equal(draft.Native.ExecutableSubdirectory, d.Native.ExecutableSubdirectory);
        Assert.Equal(draft.Native.ExecutableArguments, d.Native.ExecutableArguments);
        Assert.Equal(draft.Native.LevelName, d.Native.LevelName);
        Assert.Equal(draft.Native.StartupSuccessRegex, d.Native.StartupSuccessRegex);
        Assert.Equal(draft.Native.Platform, d.Native.Platform);
    }

    [Fact]
    public void Parse_PreservesInstancePlaceholdersAndColonsInQuotedScalars()
    {
        // The $instance_* placeholders and a port RANGE (colon inside the value) must survive — the
        // single-quote un-escaping and the anchored-key colon split are what make that work.
        var draft = FullDraft() with
        {
            Native = FullDraft().Native with
            {
                Ports = "2456:2458/tcp|2456:2458/udp",
                ExecutableArguments = "-world $instance_level_name -port 2456 -name it's",
            },
        };

        var d = _sut.TryParse(_sut.Render(draft)).Value!;

        Assert.Equal("2456:2458/tcp|2456:2458/udp", d.Native.Ports);
        // The apostrophe survives YAML single-quote escaping (rendered as ''), and $instance_* is intact.
        Assert.Equal("-world $instance_level_name -port 2456 -name it's", d.Native.ExecutableArguments);
    }

    [Fact]
    public void Parse_ToleratesHandEdits_UnquotedAndDoubleQuotedAndComments()
    {
        const string yaml = """
            # a user's hand-edited draft
            schema_version: 1
            name: handedit
            runtime: native
            metadata:
              display_name: "Hand Edit"
              max_players: 8
            native:
              ports: 7777/tcp|7777/udp
              steam_app_id: 1234
              executable_file: start.sh
              executable_arguments: -batchmode -nographics
              startup_success_regex: Server started
            """;

        var parsed = _sut.TryParse(yaml);

        Assert.True(parsed.IsOk);
        var d = parsed.Value!;
        Assert.Equal("Hand Edit", d.Metadata.DisplayName);       // double-quoted
        Assert.Equal(8, d.Metadata.MaxPlayers);
        Assert.Equal("7777/tcp|7777/udp", d.Native.Ports);        // unquoted
        Assert.Equal("start.sh", d.Native.ExecutableFile);
        Assert.Equal("-batchmode -nographics", d.Native.ExecutableArguments);
        Assert.Equal("Server started", d.Native.StartupSuccessRegex);
    }

    [Fact]
    public void Parse_NullLiterals_BecomeNullMetadata_NotFabricatedZero()
    {
        var draft = _sut.TryParse(_sut.Render(new NativeBlueprintDraft
        {
            Name = "sparse",
            Native = new NativeBlueprintNativeDraft { ExecutableFile = "run.sh" },
        })).Value!;

        Assert.Null(draft.Metadata.DisplayName);
        Assert.Null(draft.Metadata.MaxPlayers);
        Assert.Null(draft.Metadata.BaseDiskMb);
    }

    [Fact]
    public void Parse_MissingExecutableFile_IsInvalidDraft()
    {
        const string yaml = "name: broken\nruntime: native\nnative:\n  ports: 7777/tcp\n";

        var parsed = _sut.TryParse(yaml);

        Assert.False(parsed.IsOk);
        Assert.Equal(FileOpOutcome.InvalidDraft, parsed.Outcome);
    }

    [Fact]
    public void Parse_NonNativeRuntime_IsRefused()
    {
        const string yaml = "name: dockergame\nruntime: container\nnative:\n  executable_file: run.sh\n";

        var parsed = _sut.TryParse(yaml);

        Assert.False(parsed.IsOk);
        Assert.Equal(FileOpOutcome.InvalidDraft, parsed.Outcome);
    }

    [Fact]
    public void Parse_UnsafeName_IsRefused()
    {
        const string yaml = "name: ../escape\nruntime: native\nnative:\n  executable_file: run.sh\n";

        var parsed = _sut.TryParse(yaml);

        Assert.False(parsed.IsOk);
        Assert.Equal(FileOpOutcome.InvalidDraft, parsed.Outcome);
    }

    private static NativeBlueprintDraft FullDraft() => new()
    {
        Name = "roundtrip",
        Metadata = new NativeBlueprintMetadataDraft
        {
            DisplayName = "Round Trip",
            MaxPlayers = 16,
            BaseDiskMb = 2048,
        },
        Native = new NativeBlueprintNativeDraft
        {
            Ports = "7777/tcp|7777/udp",
            SteamAppId = 1000,
            ClientSteamAppId = 1001,
            Platform = "linux",
            LevelName = "world",
            ExecutableSubdirectory = "bin/x64",
            ExecutableFile = "server.x86_64",
            ExecutableArguments = "-config serverconfig.txt",
            StartupSuccessRegex = "Server started",
        },
    };
}
