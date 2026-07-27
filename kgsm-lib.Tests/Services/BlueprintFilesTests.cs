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
    private readonly string _systemDir;
    private readonly Mock<IKgsmCommandExecutor> _mockExecutor;
    private readonly Mock<IBlueprintService> _mockBlueprints;
    private readonly RecordingEventManagementService _events;
    private readonly BlueprintFiles _sut;

    public BlueprintFilesTests()
    {
        _userDir = Directory.CreateTempSubdirectory("kgsm-blueprintfiles-user-").FullName;
        _systemDir = Directory.CreateTempSubdirectory("kgsm-blueprintfiles-system-").FullName;
        _mockExecutor = new Mock<IKgsmCommandExecutor>();
        _mockBlueprints = new Mock<IBlueprintService>();
        _events = new RecordingEventManagementService();
        SetPathsOutput(_userDir);

        _sut = new BlueprintFiles(
            _mockExecutor.Object, _mockBlueprints.Object, _events, NullLogger<BlueprintFiles>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_userDir, recursive: true); } catch { /* best-effort cleanup */ }
        try { Directory.Delete(_systemDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    /// <summary>Mocks the deserialized <c>kgsm --paths --json</c> result the way the real CLI emits it
    /// (see <c>kgsm.sh</c>'s <c>_cmd_paths</c>) — only the two blueprint directories are load-bearing for
    /// the resolvers under test; the rest is realistic filler. Pass a null user dir to model a JSON payload
    /// that omits the user blueprints directory.</summary>
    private void SetPathsOutput(string? userBlueprintsDir, string? systemBlueprintsDir = null)
    {
        var paths = new KgsmPaths
        {
            System = new KgsmSystemPaths
            {
                Root = "/opt/kgsm",
                SystemBlueprintsDir = systemBlueprintsDir ?? _systemDir,
            },
            User = new KgsmUserPaths
            {
                DataDir = "/home/x/.local/share/kgsm",
                UserBlueprintsDir = userBlueprintsDir,
                UserOverridesDir = "/home/x/.local/share/kgsm/overrides",
            },
        };
        SetPathsResult(paths);
    }

    /// <summary>Mocks the engine's candidate resolution (<c>kgsm blueprints find &lt;name&gt; --json</c>)
    /// from whichever of the two temp dirs actually hold the file, so the mock never claims something the
    /// filesystem contradicts. Re-evaluated on EVERY call, exactly like the real CLI: an authority that
    /// asks again after writing must see the file it just wrote.</summary>
    private void SetCandidates(string name)
    {
        string userPath = Path.Combine(_userDir, name + ".bp.yaml");
        string systemPath = Path.Combine(_systemDir, name + ".bp.yaml");

        _mockBlueprints.Setup(x => x.FindAll(name)).Returns(() =>
        {
            bool userExists = File.Exists(userPath);
            bool systemExists = File.Exists(systemPath);

            return !userExists && !systemExists
                ? null // the engine exits non-zero with no JSON when a name resolves to nothing
                : new BlueprintCandidates
                {
                    Name = name,
                    Resolved = userExists ? userPath : systemPath,
                    Candidates =
                    [
                        new BlueprintCandidate { Tier = BlueprintTier.User, Path = userPath, Exists = userExists },
                        new BlueprintCandidate { Tier = BlueprintTier.System, Path = systemPath, Exists = systemExists },
                    ],
                };
        });
    }

    /// <summary>Mocks the engine's schema check for any path. Valid by default — an individual test opts
    /// into a rejection.</summary>
    private void SetValidation(bool valid, params string[] errors)
    {
        _mockBlueprints.Setup(x => x.Validate(It.IsAny<string>()))
            .Returns((string p) => new BlueprintValidation { Valid = valid, Path = p, Errors = [.. errors] });
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
        Assert.Throws<ArgumentNullException>(() => new BlueprintFiles(
            null!, _mockBlueprints.Object, _events, NullLogger<BlueprintFiles>.Instance));
    }

    [Fact]
    public void Constructor_NullBlueprintService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlueprintFiles(
            _mockExecutor.Object, null!, _events, NullLogger<BlueprintFiles>.Instance));
    }

    [Fact]
    public void Constructor_NullEventService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlueprintFiles(
            _mockExecutor.Object, _mockBlueprints.Object, null!, NullLogger<BlueprintFiles>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlueprintFiles(
            _mockExecutor.Object, _mockBlueprints.Object, _events, null!));
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

    // ---- ReadRaw: the read jail spans BOTH blueprint directories -----------------------------------

    /// <summary>A container blueprint with comments, blank lines, and a block scalar — the exact shape a
    /// typed round-trip destroys (Create/Render handle native only and strip every comment). ReadRaw must
    /// return it byte for byte.</summary>
    private const string ContainerBlueprintWithComments =
        "schema_version: 1\n" +
        "name: abioticfactor\n" +
        "runtime: container\n" +
        "metadata:\n" +
        "  # TODO: curate — advisory values researched per game; null = unknown/unbounded, NEVER 0\n" +
        "  display_name: \"Abiotic Factor\"\n" +
        "  max_players: 6\n" +
        "\n" +
        "container:\n" +
        "  compose: |-\n" +
        "    services:\n" +
        "      abioticfactor:\n" +
        "\n" +
        "        # Official KGSM Abiotic Factor Docker image\n" +
        "        image: ghcr.io/thekrystalship/abioticfactor:latest\n" +
        "\n" +
        "        # Use the host's network stack directly\n" +
        "        network_mode: host\n" +
        "\n" +
        "        # Ensure these ports are forwarded to allow external access\n" +
        "        ports:\n" +
        "          - 7777:7777/udp\n" +
        "          - 27015:27015/udp\n";

    private string WriteUser(string name, string content)
    {
        string path = Path.Combine(_userDir, name + ".bp.yaml");
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteSystem(string name, string content)
    {
        string path = Path.Combine(_systemDir, name + ".bp.yaml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadRaw_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.ReadRaw(null!, 1024));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("Bad-Name")]
    [InlineData("with/slash")]
    public void ReadRaw_UnsafeName_ReturnsOutOfJail(string name)
    {
        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw(name, 1024);

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
        // Refused before the engine is ever consulted.
        _mockBlueprints.Verify(x => x.FindAll(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ReadRaw_NameResolvesToNothing_ReturnsNotFound()
    {
        SetCandidates("ghost");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("ghost", 1024);

        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void ReadRaw_ContainerBlueprintWithComments_ReturnsBytesVerbatim()
    {
        WriteSystem("abioticfactor", ContainerBlueprintWithComments);
        SetCandidates("abioticfactor");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("abioticfactor", 64 * 1024);

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        Assert.Equal(ContainerBlueprintWithComments, result.Value!.Content);
    }

    [Fact]
    public void ReadRaw_SystemBlueprintWithNoUserCopy_ReportsSystemTierAndNoOverride()
    {
        WriteSystem("factorio", "schema_version: 1\nname: factorio\nruntime: native\n");
        SetCandidates("factorio");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("factorio", 1024);

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        Assert.Equal(BlueprintTier.System, result.Value!.Tier);
        Assert.True(result.Value.HasSystemOriginal);
        Assert.False(result.Value.OverridesSystem); // a system file does not override anything
    }

    [Fact]
    public void ReadRaw_UserCopyShadowingSystem_ReportsUserTierAndOverride()
    {
        WriteSystem("palworld", "schema_version: 1\nname: palworld\nruntime: native\n# shipped\n");
        WriteUser("palworld", "schema_version: 1\nname: palworld\nruntime: native\n# edited\n");
        SetCandidates("palworld");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("palworld", 1024);

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        Assert.Equal(BlueprintTier.User, result.Value!.Tier);
        Assert.True(result.Value.OverridesSystem);
        Assert.Contains("# edited", result.Value.Content); // the USER copy won, as the engine resolved it
    }

    [Fact]
    public void ReadRaw_UserOnlyBlueprint_ReportsNoSystemOriginal()
    {
        WriteUser("teamfortress2", "schema_version: 1\nname: teamfortress2\nruntime: native\n");
        SetCandidates("teamfortress2");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("teamfortress2", 1024);

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        Assert.Equal(BlueprintTier.User, result.Value!.Tier);
        // No original exists, so deleting this file would destroy the only copy rather than revert.
        Assert.False(result.Value.HasSystemOriginal);
        Assert.False(result.Value.OverridesSystem);
    }

    [Fact]
    public void ReadRaw_EngineResolvesPathOutsideBothDirs_ReturnsOutOfJail()
    {
        string outside = Path.Combine(Path.GetTempPath(), "kgsm-outside-" + Guid.NewGuid().ToString("N")[..8] + ".bp.yaml");
        File.WriteAllText(outside, "schema_version: 1\nname: sneaky\nruntime: native\n");
        try
        {
            _mockBlueprints.Setup(x => x.FindAll("sneaky")).Returns(new BlueprintCandidates
            {
                Name = "sneaky",
                Resolved = outside,
                Candidates = [new BlueprintCandidate { Tier = BlueprintTier.User, Path = outside, Exists = true }],
            });

            FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("sneaky", 1024);

            // The engine's answer is checked, not trusted.
            Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
        }
        finally { File.Delete(outside); }
    }

    [Fact]
    public void ReadRaw_FileExceedsMaxBytes_ReturnsTooLarge()
    {
        WriteUser("big", new string('x', 5000));
        SetCandidates("big");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("big", 1024);

        Assert.Equal(FileOpOutcome.TooLarge, result.Outcome);
    }

    [Fact]
    public void ReadRaw_BinaryContent_ReturnsBinary()
    {
        File.WriteAllBytes(Path.Combine(_userDir, "blob.bp.yaml"), [0x00, 0x01, 0x02, 0x03]);
        SetCandidates("blob");

        FileOpResult<BlueprintFileContent> result = _sut.ReadRaw("blob", 1024);

        Assert.Equal(FileOpOutcome.Binary, result.Outcome);
    }

    [Fact]
    public void ReadRaw_EtagRoundTripsIntoWriteRaw()
    {
        WriteUser("terraria", "schema_version: 1\nname: terraria\nruntime: native\n");
        SetCandidates("terraria");
        SetValidation(valid: true);

        FileOpResult<BlueprintFileContent> read = _sut.ReadRaw("terraria", 1024);
        FileOpResult<FileStat> write = _sut.WriteRaw("terraria",
            "schema_version: 1\nname: terraria\nruntime: native\n# edited\n",
            new BlueprintWriteOptions { MaxBytes = 1024, ExpectedEtag = read.Value!.Etag });

        Assert.Equal(FileOpOutcome.Ok, write.Outcome);
    }

    // ---- WriteRaw ----------------------------------------------------------------------------------

    private static BlueprintWriteOptions Opts(
        long maxBytes = 64 * 1024, string? etag = null, string? actor = null, string? origin = null) =>
        new() { MaxBytes = maxBytes, ExpectedEtag = etag, Actor = actor, Origin = origin };

    [Fact]
    public void WriteRaw_NullContent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.WriteRaw("factorio", null!, Opts()));
    }

    [Fact]
    public void WriteRaw_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.WriteRaw("factorio", "x", null!));
    }

    [Fact]
    public void WriteRaw_UnsafeName_ReturnsOutOfJailAndWritesNothing()
    {
        FileOpResult<FileStat> result = _sut.WriteRaw("../escape", "content", Opts());

        Assert.Equal(FileOpOutcome.OutOfJail, result.Outcome);
        Assert.Empty(Directory.GetFiles(_userDir));
        Assert.Empty(_events.Emissions);
    }

    [Fact]
    public void WriteRaw_ContentExceedsMaxBytes_ReturnsTooLarge()
    {
        FileOpResult<FileStat> result = _sut.WriteRaw("big", new string('x', 500), Opts(maxBytes: 100));

        Assert.Equal(FileOpOutcome.TooLarge, result.Outcome);
        Assert.Empty(Directory.GetFiles(_userDir));
    }

    [Fact]
    public void WriteRaw_ContainerBlueprintWithComments_WritesBytesVerbatim()
    {
        SetCandidates("abioticfactor");
        SetValidation(valid: true);

        FileOpResult<FileStat> result = _sut.WriteRaw("abioticfactor", ContainerBlueprintWithComments, Opts());

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        // Byte-identical: comments, blank lines, block scalar and field order all survive.
        Assert.Equal(ContainerBlueprintWithComments, File.ReadAllText(Path.Combine(_userDir, "abioticfactor.bp.yaml")));
    }

    [Fact]
    public void WriteRaw_EditingASystemBlueprint_CreatesUserOverrideAndLeavesTheOriginalUntouched()
    {
        const string shipped = "schema_version: 1\nname: factorio\nruntime: native\n";
        string systemPath = WriteSystem("factorio", shipped);
        SetCandidates("factorio");
        SetValidation(valid: true);

        FileOpResult<FileStat> result = _sut.WriteRaw("factorio", shipped + "# my edit\n", Opts());

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        Assert.Equal(shipped, File.ReadAllText(systemPath)); // the shipped file is never written to
        Assert.Contains("# my edit", File.ReadAllText(Path.Combine(_userDir, "factorio.bp.yaml")));
    }

    [Fact]
    public void WriteRaw_EngineRejectsContent_ReturnsInvalidDraftWithErrorsAndWritesNothing()
    {
        SetCandidates("broken");
        SetValidation(valid: false, "Blueprint has invalid or missing 'runtime'", "and another problem");

        FileOpResult<FileStat> result = _sut.WriteRaw("broken", "name: broken\n", Opts());

        Assert.Equal(FileOpOutcome.InvalidDraft, result.Outcome);
        Assert.Contains("invalid or missing 'runtime'", result.Message);
        Assert.Contains("and another problem", result.Message); // every error, not just the first
        // Neither the real filename NOR the temp file survives a rejection.
        Assert.Empty(Directory.GetFileSystemEntries(_userDir));
        Assert.Empty(_events.Emissions);
    }

    [Fact]
    public void WriteRaw_Rejection_KeepsTheEngineErrorsAsAList_NotOnlyAJoinedMessage()
    {
        SetCandidates("broken");
        SetValidation(valid: false, "first problem", "second problem");

        FileOpResult<FileStat> result = _sut.WriteRaw("broken", "name: broken\n", Opts());

        // A surface that renders one bullet per error must not have to split Message back apart.
        Assert.Equal(["first problem", "second problem"], result.Errors);
    }

    [Fact]
    public void WriteRaw_NonValidationFailure_CarriesNoErrorList()
    {
        SetCandidates("factorio");
        SetValidation(valid: true);

        FileOpResult<FileStat> result = _sut.WriteRaw(
            "factorio", "name: factorio\n", Opts(etag: "sha256:notthecurrentone"));

        Assert.Equal(FileOpOutcome.EtagMismatch, result.Outcome);
        Assert.Empty(result.Errors); // only a multi-reason failure populates the list
    }

    [Fact]
    public void WriteRaw_RejectionMessage_NamesTheBlueprintNotTheTempFile()
    {
        SetCandidates("broken");
        // The engine appends the path it judged to each error — which here is a temp file the caller
        // never chose and cannot act on.
        _mockBlueprints.Setup(x => x.Validate(It.IsAny<string>()))
            .Returns((string p) => new BlueprintValidation
            {
                Valid = false,
                Path = p,
                Errors = [$"Blueprint has invalid or missing 'runtime' (expected native|container): {p}"],
            });

        FileOpResult<FileStat> result = _sut.WriteRaw("broken", "name: broken\n", Opts());

        Assert.Equal(FileOpOutcome.InvalidDraft, result.Outcome);
        Assert.Contains("broken.bp.yaml", result.Message);
        Assert.DoesNotContain(".tmp", result.Message);
    }

    [Fact]
    public void WriteRaw_EngineReturnsNoVerdict_RefusesRatherThanAssumingValid()
    {
        SetCandidates("unknown");
        _mockBlueprints.Setup(x => x.Validate(It.IsAny<string>())).Returns((BlueprintValidation?)null);

        FileOpResult<FileStat> result = _sut.WriteRaw("unknown", "name: unknown\n", Opts());

        Assert.Equal(FileOpOutcome.BlueprintsDirUnavailable, result.Outcome);
        Assert.Empty(Directory.GetFileSystemEntries(_userDir));
    }

    [Fact]
    public void WriteRaw_ValidationRunsOnATempFileTheEngineGlobCannotSee()
    {
        SetCandidates("hidden");
        string? validatedPath = null;
        _mockBlueprints.Setup(x => x.Validate(It.IsAny<string>()))
            .Returns((string p) =>
            {
                validatedPath = p;
                return new BlueprintValidation { Valid = true, Path = p };
            });

        _sut.WriteRaw("hidden", "schema_version: 1\nname: hidden\nruntime: native\n", Opts());

        Assert.NotNull(validatedPath);
        string validatedName = Path.GetFileName(validatedPath!);
        Assert.NotEqual("hidden.bp.yaml", validatedName);
        Assert.StartsWith(".", validatedName);       // dotfile — excluded from the engine's glob
        Assert.EndsWith(".tmp", validatedName);      // and not a *.bp.yaml name either
    }

    [Fact]
    public void WriteRaw_StaleExpectedEtag_ReturnsEtagMismatchAndLeavesTheFileAlone()
    {
        const string onDisk = "schema_version: 1\nname: terraria\nruntime: native\n";
        WriteUser("terraria", onDisk);
        SetCandidates("terraria");
        SetValidation(valid: true);

        FileOpResult<FileStat> result = _sut.WriteRaw("terraria", "# clobbered\n",
            Opts(etag: "sha256:0000000000000000000000000000000000000000000000000000000000000000"));

        Assert.Equal(FileOpOutcome.EtagMismatch, result.Outcome);
        Assert.Equal(onDisk, File.ReadAllText(Path.Combine(_userDir, "terraria.bp.yaml")));
        Assert.Empty(_events.Emissions);
    }

    [Fact]
    public void WriteRaw_ExpectedEtagGuardsTheRESOLVEDFileNotTheUserTarget()
    {
        // The first override of a shipped blueprint: the caller read the SYSTEM file, so that is the
        // etag they hold, and it is what the guard has to compare against — the user target does not
        // exist yet.
        const string shipped = "schema_version: 1\nname: factorio\nruntime: native\n";
        WriteSystem("factorio", shipped);
        SetCandidates("factorio");
        SetValidation(valid: true);
        string systemEtag = _sut.ReadRaw("factorio", 1024).Value!.Etag;

        FileOpResult<FileStat> result = _sut.WriteRaw("factorio", shipped + "# override\n", Opts(etag: systemEtag));

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
    }

    // ---- events ------------------------------------------------------------------------------------

    [Fact]
    public void WriteRaw_NewUserFile_EmitsBlueprintCreatedWithProvenance()
    {
        SetCandidates("necesse");
        SetValidation(valid: true);

        _sut.WriteRaw("necesse", "schema_version: 1\nname: necesse\nruntime: native\n",
            Opts(actor: "discord:987654321", origin: "ui"));

        RecordingEventManagementService.Emission emitted = _events.Single();
        Assert.Equal("blueprint-created", emitted.EventType);
        Assert.Equal("discord:987654321", emitted.Actor);
        Assert.Equal("ui", emitted.Origin);
        Assert.Equal(["necesse", "user", "false", "native"], emitted.Parameters);
    }

    [Fact]
    public void WriteRaw_ExistingUserFile_EmitsBlueprintUpdated()
    {
        WriteUser("necesse", "schema_version: 1\nname: necesse\nruntime: native\n");
        SetCandidates("necesse");
        SetValidation(valid: true);

        _sut.WriteRaw("necesse", "schema_version: 1\nname: necesse\nruntime: native\n# v2\n", Opts());

        Assert.Equal("blueprint-updated", _events.Single().EventType);
    }

    [Fact]
    public void WriteRaw_OverridingAShippedBlueprint_EmitsOverridesSystemTrue()
    {
        WriteSystem("palworld", "schema_version: 1\nname: palworld\nruntime: container\n");
        SetCandidates("palworld");
        SetValidation(valid: true);

        _sut.WriteRaw("palworld", "schema_version: 1\nname: palworld\nruntime: container\n# mine\n", Opts());

        RecordingEventManagementService.Emission emitted = _events.Single();
        Assert.Equal("blueprint-created", emitted.EventType); // no USER file existed before
        Assert.Equal(["palworld", "user", "true", "container"], emitted.Parameters);
    }

    [Fact]
    public void WriteRaw_ContentWithNoReadableRuntime_EmitsAnEmptyRuntimeRatherThanGuessing()
    {
        SetCandidates("odd");
        SetValidation(valid: true); // the engine is the authority; this test is about the emitter

        _sut.WriteRaw("odd", "schema_version: 1\nname: odd\n", Opts());

        // The engine renders an empty argument as JSON null — unknown, never a defaulted "native".
        Assert.Equal(string.Empty, _events.Single().Parameters[3]);
    }

    [Fact]
    public void WriteRaw_IndentedRuntimeKey_IsNotMistakenForTheTopLevelOne()
    {
        SetCandidates("nested");
        SetValidation(valid: true);

        _sut.WriteRaw("nested", "schema_version: 1\nname: nested\ncontainer:\n  runtime: sneaky\n", Opts());

        Assert.Equal(string.Empty, _events.Single().Parameters[3]);
    }

    [Fact]
    public void WriteRaw_FailedEmit_DoesNotFailTheWrite()
    {
        SetCandidates("necesse");
        SetValidation(valid: true);
        _events.Throws = new InvalidOperationException("engine unreachable");

        FileOpResult<FileStat> result = _sut.WriteRaw("necesse", "schema_version: 1\nname: necesse\nruntime: native\n", Opts());

        // The bytes are committed and valid — reporting a failure here would claim the save did not happen.
        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        Assert.True(File.Exists(Path.Combine(_userDir, "necesse.bp.yaml")));
    }

    [Fact]
    public void Remove_WithAShippedOriginal_EmitsRevertedToSystemTrue()
    {
        WriteSystem("palworld", "schema_version: 1\nname: palworld\nruntime: native\n");
        WriteUser("palworld", "schema_version: 1\nname: palworld\nruntime: native\n# mine\n");
        SetCandidates("palworld");

        FileOpResult result = _sut.Remove("palworld", actor: "user:heisen", origin: "api");

        Assert.Equal(FileOpOutcome.Ok, result.Outcome);
        RecordingEventManagementService.Emission emitted = _events.Single();
        Assert.Equal("blueprint-removed", emitted.EventType);
        Assert.Equal("user:heisen", emitted.Actor);
        Assert.Equal("api", emitted.Origin);
        Assert.Equal(["palworld", "user", "true"], emitted.Parameters);
    }

    [Fact]
    public void Remove_WithNoShippedOriginal_EmitsRevertedToSystemFalse()
    {
        WriteUser("teamfortress2", "schema_version: 1\nname: teamfortress2\nruntime: native\n");
        SetCandidates("teamfortress2");

        _sut.Remove("teamfortress2");

        // Nothing was restored — the blueprint is gone entirely, and the event says so.
        Assert.Equal(["teamfortress2", "user", "false"], _events.Single().Parameters);
    }

    [Fact]
    public void Remove_MissingFile_EmitsNothing()
    {
        SetCandidates("ghost");

        FileOpResult result = _sut.Remove("ghost");

        Assert.Equal(FileOpOutcome.NotFound, result.Outcome);
        Assert.Empty(_events.Emissions);
    }

    [Fact]
    public void Create_EmitsBlueprintCreatedToo()
    {
        SetCandidates("templated");

        _sut.Create(MinimalDraft("templated"), overwrite: false, actor: "assistant", origin: "assistant");

        RecordingEventManagementService.Emission emitted = _events.Single();
        Assert.Equal("blueprint-created", emitted.EventType);
        Assert.Equal("assistant", emitted.Actor);
        Assert.Equal(["templated", "user", "false", "native"], emitted.Parameters);
    }

    [Fact]
    public void Create_OverwritingAnExistingUserFile_EmitsBlueprintUpdated()
    {
        WriteUser("templated", "schema_version: 1\nname: templated\nruntime: native\n");
        SetCandidates("templated");

        _sut.Create(MinimalDraft("templated"), overwrite: true);

        Assert.Equal("blueprint-updated", _events.Single().EventType);
    }
}
