using System.Text.Json;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the InstanceService class.
///
/// InstanceService is a facade over two collaborators: <see cref="IKgsmCommandExecutor"/>
/// (for direct <c>instances</c> commands and JSON reads) and <see cref="ILifecycleService"/>
/// (for operational verbs — start/stop/restart/status/is-active/logs). Tests here assert
/// the facade's own responsibilities: input validation, the exact command it issues, and
/// that it forwards lifecycle calls. The behavioral contract of the lifecycle verbs
/// (failure channels, log splitting) is covered in <see cref="LifecycleServiceTests"/>.
///
/// Failure-channel convention asserted below: methods returning <see cref="KgsmResult"/>
/// encode failure in the result (<c>IsSuccess == false</c>) and never throw on a non-zero
/// exit; methods returning a nullable type return null on failure.
/// </summary>
public class InstanceServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogSubscriptionService> _mockLogSubscriptionService;
    private readonly Mock<ILifecycleService> _mockLifecycleService;
    private readonly Mock<ILogger<InstanceService>> _mockLogger;
    private readonly InstanceService _instanceService;

    private const string Instance = "my-server";

    public InstanceServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogSubscriptionService = new Mock<ILogSubscriptionService>();
        _mockLifecycleService = new Mock<ILifecycleService>();
        _mockLogger = new Mock<ILogger<InstanceService>>();
        _instanceService = new InstanceService(
            _mockCommandExecutor.Object,
            _mockLogSubscriptionService.Object,
            _mockLifecycleService.Object,
            _mockLogger.Object);
    }

    private static bool ArgsAre(string[] actual, params string[] expected)
        => actual.SequenceEqual(expected);

    // --- Constructor guards ---

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InstanceService(
            null!, _mockLogSubscriptionService.Object, _mockLifecycleService.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogSubscriptionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InstanceService(
            _mockCommandExecutor.Object, null!, _mockLifecycleService.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLifecycleService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InstanceService(
            _mockCommandExecutor.Object, _mockLogSubscriptionService.Object, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InstanceService(
            _mockCommandExecutor.Object, _mockLogSubscriptionService.Object, _mockLifecycleService.Object, null!));
    }

    // --- GetAll : ExecuteForJson<Dictionary<string, Instance>>("instances list --detailed --json") ---

    [Fact]
    public void GetAll_SuccessfulExecution_ReturnsInstances()
    {
        var expected = new Dictionary<string, Instance>
        {
            [Instance] = new Instance { Name = Instance }
        };
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Instance>>(
                It.Is<string[]>(a => ArgsAre(a, "instances", "list", "--detailed", "--json")),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Instance>?>()))
            .Returns(expected);

        Dictionary<string, Instance> result = _instanceService.GetAll();

        Assert.Single(result);
        Assert.Equal(Instance, result[Instance].Name);
    }

    [Fact]
    public void GetAll_CommandReturnsNull_ReturnsEmptyDictionary()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Instance>>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Dictionary<string, Instance>?>()))
            .Returns((Dictionary<string, Instance>?)null);

        Dictionary<string, Instance> result = _instanceService.GetAll();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // --- GetInstanceInfo : ExecuteForJson<Instance>("instances info <name> --json") ---

    [Fact]
    public void GetInstanceInfo_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetInstanceInfo(null!));
    }

    [Fact]
    public void GetInstanceInfo_SuccessfulExecution_ReturnsInstance()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Instance>(
                It.Is<string[]>(a => ArgsAre(a, "instances", "info", Instance, "--json")),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Instance?>()))
            .Returns(new Instance { Name = Instance });

        Instance? result = _instanceService.GetInstanceInfo(Instance);

        Assert.NotNull(result);
        Assert.Equal(Instance, result!.Name);
    }

    [Fact]
    public void GetInstanceInfo_ExecutionFails_ReturnsNull()
    {
        // Instance? return type: failure is signalled by null, not an exception.
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Instance>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<Instance?>()))
            .Returns((Instance?)null);

        Assert.Null(_instanceService.GetInstanceInfo(Instance));
    }

    // --- GetInstanceStatus : ExecuteForJson<InstanceRuntimeStatus>("instances status <name> --json") ---

    [Fact]
    public void GetInstanceStatus_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetInstanceStatus(null!));
    }

    [Fact]
    public void GetInstanceStatus_SuccessfulExecution_ReturnsStatus()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<InstanceRuntimeStatus>(
                It.Is<string[]>(a => ArgsAre(a, "instances", "status", Instance, "--json")),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<InstanceRuntimeStatus?>()))
            .Returns(new InstanceRuntimeStatus { InstanceName = Instance, Status = true });

        InstanceRuntimeStatus? result = _instanceService.GetInstanceStatus(Instance);

        Assert.NotNull(result);
        Assert.Equal(Instance, result!.InstanceName);
        Assert.True(result.Status);
    }

    [Fact]
    public void GetInstanceStatus_ExecutionFails_ReturnsNull()
    {
        // InstanceRuntimeStatus? return type: failure is null, not a KgsmException.
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<InstanceRuntimeStatus>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>?>(),
                It.IsAny<InstanceRuntimeStatus?>()))
            .Returns((InstanceRuntimeStatus?)null);

        Assert.Null(_instanceService.GetInstanceStatus(Instance));
    }

    // --- GetAllStatuses (bulk fleet read) : uses the injected command executor directly ---

    [Fact]
    public void GetAllStatuses_Default_RequestsNonFastBulkStatus()
    {
        var expected = new Dictionary<string, Reading<InstanceRuntimeStatus>>
        {
            ["7dtd"] = Reading<InstanceRuntimeStatus>.Measured(new() { InstanceName = "7dtd", Status = true })
        };
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                It.Is<string[]>(a => ArgsAre(a, "instances", "list", "--status", "--json")),
                It.IsAny<Action<JsonSerializerOptions>>(),
                It.IsAny<Dictionary<string, Reading<InstanceRuntimeStatus>>>()))
            .Returns(expected);

        var result = _instanceService.GetAllStatuses();

        Assert.Same(expected, result);
        Assert.Equal(ReadingState.Measured, result["7dtd"].State);
        Assert.True(result["7dtd"].Value!.Status);
    }

    [Fact]
    public void GetAllStatuses_Fast_AppendsFastFlag()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                It.Is<string[]>(a => ArgsAre(a, "instances", "list", "--status", "--json", "--fast")),
                It.IsAny<Action<JsonSerializerOptions>>(),
                It.IsAny<Dictionary<string, Reading<InstanceRuntimeStatus>>>()))
            .Returns(new Dictionary<string, Reading<InstanceRuntimeStatus>>());

        var result = _instanceService.GetAllStatuses(fast: true);

        Assert.NotNull(result);
        _mockCommandExecutor.Verify(x => x.ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
            It.Is<string[]>(a => a.Contains("--fast")),
            It.IsAny<Action<JsonSerializerOptions>>(),
            It.IsAny<Dictionary<string, Reading<InstanceRuntimeStatus>>>()), Times.Once);
    }

    [Fact]
    public void GetAllStatuses_CommandReturnsNull_ReturnsEmptyDictionary()
    {
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson<Dictionary<string, Reading<InstanceRuntimeStatus>>>(
                It.IsAny<string[]>(),
                It.IsAny<Action<JsonSerializerOptions>>(),
                It.IsAny<Dictionary<string, Reading<InstanceRuntimeStatus>>>()))
            .Returns((Dictionary<string, Reading<InstanceRuntimeStatus>>?)null);

        var result = _instanceService.GetAllStatuses();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // --- Install : Execute(timeout, "install", blueprint, [--install-dir, --version, --name]) ---

    [Fact]
    public void Install_NullBlueprintName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.Install(null!));
    }

    [Fact]
    public void Install_ValidBlueprint_IssuesInstallCommand()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.IsAny<TimeSpan>(),
                It.Is<string[]>(a => ArgsAre(a, "install", "valheim"))))
            .Returns(new KgsmResult(new ProcessResult(0, "installed", string.Empty)));

        KgsmResult result = _instanceService.Install("valheim");

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(x => x.Execute(
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "install", "valheim"))), Times.Once);
    }

    [Fact]
    public void Install_WithAllParameters_PassesEveryFlag()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.IsAny<TimeSpan>(),
                It.Is<string[]>(a => ArgsAre(a,
                    "install", "valheim",
                    "--install-dir", "/custom/path",
                    "--version", "1.0.0",
                    "--name", "my-server"))))
            .Returns(new KgsmResult(new ProcessResult(0, "installed", string.Empty)));

        KgsmResult result = _instanceService.Install("valheim", "/custom/path", "1.0.0", "my-server");

        Assert.True(result.IsSuccess);
    }

    // --- Uninstall : Execute(timeout, "uninstall", name, "--force") ---
    // Always --force: a programmatic uninstall is already confirmed at the calling surface, and kgsm's
    // uninstall is interactive-by-default (a no-TTY call without --force is a non-zero EC_CANCELLED no-op).

    [Fact]
    public void Uninstall_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.Uninstall(null!));
    }

    [Fact]
    public void Uninstall_ValidInstance_IssuesForcedUninstallCommand()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.IsAny<TimeSpan>(),
                It.Is<string[]>(a => ArgsAre(a, "uninstall", Instance, "--force"))))
            .Returns(new KgsmResult(new ProcessResult(0, "uninstalled", string.Empty)));

        KgsmResult result = _instanceService.Uninstall(Instance);

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(x => x.Execute(
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "uninstall", Instance, "--force"))), Times.Once);
    }

    // --- GetInfo : Execute("instances", "info", name) (raw, not JSON) ---

    [Fact]
    public void GetInfo_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetInfo(null!));
    }

    [Fact]
    public void GetInfo_ValidInstance_IssuesInfoCommand()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "info", Instance))))
            .Returns(new KgsmResult(new ProcessResult(0, "info...", string.Empty)));

        KgsmResult result = _instanceService.GetInfo(Instance);

        Assert.True(result.IsSuccess);
    }

    // --- Delegation to ILifecycleService (thin forwarding; behavior lives in LifecycleServiceTests) ---

    [Fact]
    public void GetStatus_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetStatus(null!));
    }

    [Fact]
    public void GetStatus_ValidInstance_ForwardsToLifecycle()
    {
        var expected = new KgsmResult(new ProcessResult(0, "status", string.Empty));
        _mockLifecycleService.Setup(x => x.GetStatus(Instance)).Returns(expected);

        KgsmResult result = _instanceService.GetStatus(Instance);

        Assert.Same(expected, result);
        _mockLifecycleService.Verify(x => x.GetStatus(Instance), Times.Once);
    }

    [Fact]
    public void IsActive_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.IsActive(null!));
    }

    [Fact]
    public void IsActive_ValidInstance_ForwardsLifecycleResult()
    {
        _mockLifecycleService.Setup(x => x.IsActive(Instance)).Returns(true);

        Assert.True(_instanceService.IsActive(Instance));
        _mockLifecycleService.Verify(x => x.IsActive(Instance), Times.Once);
    }

    [Fact]
    public void GetLogs_NullInstanceName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetLogs(null!));
    }

    [Fact]
    public void GetLogs_ValidInstance_ForwardsToLifecycle()
    {
        ICollection<string> expected = new[] { "line 1", "line 2" };
        _mockLifecycleService.Setup(x => x.GetLogs(Instance, It.IsAny<int>())).Returns(expected);

        ICollection<string> result = _instanceService.GetLogs(Instance);

        Assert.Same(expected, result);
        _mockLifecycleService.Verify(x => x.GetLogs(Instance, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetLogsAsync_NullInstanceName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _instanceService.GetLogsAsync(null!));
    }

    [Fact]
    public async Task GetLogsAsync_ValidInstance_ForwardsToLifecycle()
    {
        ICollection<string> expected = new[] { "line 1" };
        _mockLifecycleService
            .Setup(x => x.GetLogsAsync(Instance, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        ICollection<string> result = await _instanceService.GetLogsAsync(Instance);

        Assert.Same(expected, result);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("restart")]
    public void LifecycleVerb_NullInstanceName_ThrowsArgumentNullException(string verb)
    {
        Func<KgsmResult> act = verb switch
        {
            "start" => () => _instanceService.Start(null!),
            "stop" => () => _instanceService.Stop(null!),
            _ => () => _instanceService.Restart(null!),
        };

        Assert.Throws<ArgumentNullException>(() => act());
    }

    [Fact]
    public void Start_ValidInstance_ForwardsToLifecycle()
    {
        var expected = new KgsmResult(new ProcessResult(0, "started", string.Empty));
        _mockLifecycleService.Setup(x => x.Start(Instance, It.IsAny<string?>(), It.IsAny<string?>())).Returns(expected);

        Assert.Same(expected, _instanceService.Start(Instance));
        _mockLifecycleService.Verify(x => x.Start(Instance, It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void Stop_ValidInstance_ForwardsToLifecycle()
    {
        var expected = new KgsmResult(new ProcessResult(0, "stopped", string.Empty));
        _mockLifecycleService.Setup(x => x.Stop(Instance, It.IsAny<string?>(), It.IsAny<string?>())).Returns(expected);

        Assert.Same(expected, _instanceService.Stop(Instance));
    }

    [Fact]
    public void Restart_ValidInstance_ForwardsToLifecycle()
    {
        var expected = new KgsmResult(new ProcessResult(0, "restarted", string.Empty));
        _mockLifecycleService.Setup(x => x.Restart(Instance, It.IsAny<string?>(), It.IsAny<string?>())).Returns(expected);

        Assert.Same(expected, _instanceService.Restart(Instance));
    }

    // --- GenerateId : Execute("instances", "generate-id", blueprint, [--name, custom]) ---

    [Fact]
    public void GenerateId_NullBlueprintName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GenerateId(null!));
    }

    [Fact]
    public void GenerateId_WhitespaceBlueprintName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _instanceService.GenerateId("   "));
    }

    [Fact]
    public void GenerateId_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "generate-id", "valheim"))))
            .Returns(new KgsmResult(new ProcessResult(0, "valheim-abc", string.Empty)));

        KgsmResult result = _instanceService.GenerateId("valheim");

        Assert.True(result.IsSuccess);
        Assert.Equal("valheim-abc", result.Stdout);
    }

    [Fact]
    public void GenerateId_WithCustomName_PassesNameFlag()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a =>
                ArgsAre(a, "instances", "generate-id", "valheim", "--name", "my-valheim"))))
            .Returns(new KgsmResult(new ProcessResult(0, "my-valheim", string.Empty)));

        KgsmResult result = _instanceService.GenerateId("valheim", "my-valheim");

        Assert.True(result.IsSuccess);
        Assert.Equal("my-valheim", result.Stdout);
    }

    [Fact]
    public void GenerateId_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "generate-id", "unknown-blueprint"))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Blueprint not found")));

        KgsmResult result = _instanceService.GenerateId("unknown-blueprint");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- Save : Execute("instances", "save", name) ---

    [Fact]
    public void Save_NullInstanceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.Save(null!));
    }

    [Fact]
    public void Save_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "save", "my-instance"))))
            .Returns(new KgsmResult(new ProcessResult(0, "Saved", string.Empty)));

        KgsmResult result = _instanceService.Save("my-instance");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Save_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "save", "my-instance"))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Instance not running")));

        KgsmResult result = _instanceService.Save("my-instance");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- SendInput : Execute("instances", "input", name, command) ---

    [Fact]
    public void SendInput_NullInstanceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.SendInput(null!, "say hello"));
    }

    [Fact]
    public void SendInput_NullCommand_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.SendInput("my-instance", null!));
    }

    [Fact]
    public void SendInput_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "input", "my-instance", "say hello"))))
            .Returns(new KgsmResult(new ProcessResult(0, "[INFO] hello", string.Empty)));

        KgsmResult result = _instanceService.SendInput("my-instance", "say hello");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SendInput_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "input", "my-instance", "say hello"))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Instance not running")));

        KgsmResult result = _instanceService.SendInput("my-instance", "say hello");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- FindConfigPath : Execute("instances", "find", name) ---

    [Fact]
    public void FindConfigPath_NullInstanceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.FindConfigPath(null!));
    }

    [Fact]
    public void FindConfigPath_SuccessfulExecution_ReturnsSuccessResult()
    {
        const string expectedPath = "/home/kgsm/instances/my-instance/my-instance.ini";
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "find", "my-instance"))))
            .Returns(new KgsmResult(new ProcessResult(0, expectedPath, string.Empty)));

        KgsmResult result = _instanceService.FindConfigPath("my-instance");

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPath, result.Stdout);
    }

    [Fact]
    public void FindConfigPath_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "find", "unknown-instance"))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Instance not found")));

        KgsmResult result = _instanceService.FindConfigPath("unknown-instance");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- GetInstanceConfigValue : Execute("instances", "config-get", name, key) ---

    [Fact]
    public void GetInstanceConfigValue_NullInstanceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetInstanceConfigValue(null!, "auto_update"));
    }

    [Fact]
    public void GetInstanceConfigValue_NullKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.GetInstanceConfigValue("my-instance", null!));
    }

    [Fact]
    public void GetInstanceConfigValue_SuccessfulExecution_ReturnsValue()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "config-get", "my-instance", "auto_update"))))
            .Returns(new KgsmResult(new ProcessResult(0, "true", string.Empty)));

        KgsmResult result = _instanceService.GetInstanceConfigValue("my-instance", "auto_update");

        Assert.True(result.IsSuccess);
        Assert.Equal("true", result.Stdout);
    }

    [Fact]
    public void GetInstanceConfigValue_ExecutionFails_ReturnsFailureResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "config-get", "unknown-instance", "auto_update"))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Instance not found")));

        KgsmResult result = _instanceService.GetInstanceConfigValue("unknown-instance", "auto_update");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // --- SetInstanceConfigValue : Execute("instances", "config-set", name, "key=value") ---

    [Fact]
    public void SetInstanceConfigValue_NullInstanceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.SetInstanceConfigValue(null!, "auto_update", "true"));
    }

    [Fact]
    public void SetInstanceConfigValue_NullKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.SetInstanceConfigValue("my-instance", null!, "true"));
    }

    [Fact]
    public void SetInstanceConfigValue_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _instanceService.SetInstanceConfigValue("my-instance", "auto_update", null!));
    }

    [Fact]
    public void SetInstanceConfigValue_SuccessfulExecution_ReturnsSuccessResult()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "config-set", "my-instance", "auto_update=true"))))
            .Returns(new KgsmResult(new ProcessResult(0, "[SUCCESS] Set 'auto_update'", string.Empty)));

        KgsmResult result = _instanceService.SetInstanceConfigValue("my-instance", "auto_update", "true");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetInstanceConfigValue_ValueWithEqualsAndSpaces_PassedAsSingleArgvElement()
    {
        // The value contains spaces and an embedded '='; it must be joined to the
        // key as one argv element so kgsm can split it on the first '=' only.
        const string value = "--foo=bar baz";
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "config-set", "my-instance", "executable_arguments=--foo=bar baz"))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        KgsmResult result = _instanceService.SetInstanceConfigValue("my-instance", "executable_arguments", value);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetInstanceConfigValue_EmptyValue_IsAllowedAndProducesTrailingEquals()
    {
        // Clearing a value is valid: key= with nothing after the '='.
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "config-set", "my-instance", "executable_arguments="))))
            .Returns(new KgsmResult(new ProcessResult(0, string.Empty, string.Empty)));

        KgsmResult result = _instanceService.SetInstanceConfigValue("my-instance", "executable_arguments", string.Empty);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetInstanceConfigValue_RefusedKey_ReturnsFailureResultNotException()
    {
        // A protected key is refused by kgsm with a non-zero exit code; the library
        // surfaces that as a failed result rather than throwing.
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "instances", "config-set", "my-instance", "name=hacked"))))
            .Returns(new KgsmResult(new ProcessResult(8, string.Empty, "'name' is a protected key")));

        KgsmResult result = _instanceService.SetInstanceConfigValue("my-instance", "name", "hacked");

        Assert.False(result.IsSuccess);
        Assert.Equal(8, result.ExitCode);
    }

    // --- provenance (1.15.0): actor/origin propagate as KGSM_EVENT_* env on every mutation ----------
    // The long-running verbs (install/uninstall/update/backup/restore) take the env+timeout overload;
    // config-set takes the default-timeout env overload; start/stop/restart forward to the lifecycle
    // layer. A null/empty value is omitted (KGSM keeps its honest fallback), and with NO provenance the
    // plain (no-env) path is taken so existing behaviour is unchanged.

    private static readonly KgsmResult Ok = new(new ProcessResult(0, "ok", string.Empty));

    private void SetupEnvTimeout() => _mockCommandExecutor
        .Setup(x => x.Execute(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
        .Returns(Ok);

    [Fact]
    public void Install_WithProvenance_StampsActorAndOriginOnTheEnvTimeoutOverload()
    {
        SetupEnvTimeout();

        _instanceService.Install("valheim", actor: "discord:haru", origin: "discord");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e =>
                e["KGSM_EVENT_ACTOR"] == "discord:haru" && e["KGSM_EVENT_ORIGIN"] == "discord"),
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "install", "valheim"))), Times.Once);
    }

    [Fact]
    public void Install_WithoutProvenance_TakesThePlainTimeoutPath_NoEnvOverload()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Returns(Ok);

        _instanceService.Install("valheim");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.IsAny<TimeSpan>(), It.Is<string[]>(a => ArgsAre(a, "install", "valheim"))), Times.Once);
        _mockCommandExecutor.Verify(x => x.Execute(
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void Uninstall_WithProvenance_StampsEnv()
    {
        SetupEnvTimeout();

        _instanceService.Uninstall("valheim", actor: "discord:haru", origin: "discord");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e => e["KGSM_EVENT_ORIGIN"] == "discord"),
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "uninstall", "valheim", "--force"))), Times.Once);
    }

    [Fact]
    public void Update_WithProvenance_StampsEnv()
    {
        SetupEnvTimeout();

        _instanceService.Update("valheim", actor: "discord:haru", origin: "assistant");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e =>
                e["KGSM_EVENT_ACTOR"] == "discord:haru" && e["KGSM_EVENT_ORIGIN"] == "assistant"),
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "instances", "update", "valheim"))), Times.Once);
    }

    [Fact]
    public void CreateBackup_WithProvenance_StampsEnv()
    {
        SetupEnvTimeout();

        _instanceService.CreateBackup("valheim", actor: "discord:haru", origin: "discord");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e => e["KGSM_EVENT_ACTOR"] == "discord:haru"),
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "instances", "create-backup", "valheim"))), Times.Once);
    }

    [Fact]
    public void RestoreBackup_WithProvenance_StampsEnv()
    {
        SetupEnvTimeout();

        _instanceService.RestoreBackup("valheim", "backup-1", actor: "discord:haru", origin: "discord");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e => e["KGSM_EVENT_ORIGIN"] == "discord"),
            It.IsAny<TimeSpan>(),
            It.Is<string[]>(a => ArgsAre(a, "instances", "restore-backup", "valheim", "backup-1"))), Times.Once);
    }

    [Fact]
    public void SetInstanceConfigValue_WithProvenance_UsesTheDefaultTimeoutEnvOverload()
    {
        // config-set is quick → the env overload WITHOUT an explicit timeout, not the env+timeout one.
        _mockCommandExecutor
            .Setup(x => x.Execute(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string[]>()))
            .Returns(Ok);

        _instanceService.SetInstanceConfigValue("valheim", "key", "val", actor: "discord:haru", origin: "discord");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e =>
                e["KGSM_EVENT_ACTOR"] == "discord:haru" && e["KGSM_EVENT_ORIGIN"] == "discord"),
            It.Is<string[]>(a => ArgsAre(a, "instances", "config-set", "valheim", "key=val"))), Times.Once);
    }

    [Fact]
    public void OnlyActor_OmitsOriginFromTheEnv_NeverFabricated()
    {
        SetupEnvTimeout();

        _instanceService.CreateBackup("valheim", actor: "system:watchdog");

        _mockCommandExecutor.Verify(x => x.Execute(
            It.Is<IReadOnlyDictionary<string, string>>(e =>
                e["KGSM_EVENT_ACTOR"] == "system:watchdog" && !e.ContainsKey("KGSM_EVENT_ORIGIN")),
            It.IsAny<TimeSpan>(), It.IsAny<string[]>()), Times.Once);
    }

    [Fact]
    public void Start_WithProvenance_ForwardsActorOriginToTheLifecycleLayer()
    {
        _instanceService.Start("valheim", actor: "discord:haru", origin: "discord");
        _mockLifecycleService.Verify(x => x.Start("valheim", "discord:haru", "discord"), Times.Once);
    }

    [Fact]
    public void StopAndRestart_WithProvenance_ForwardToTheLifecycleLayer()
    {
        _instanceService.Stop("valheim", actor: "discord:haru", origin: "assistant");
        _instanceService.Restart("valheim", actor: "api:token", origin: "api");
        _mockLifecycleService.Verify(x => x.Stop("valheim", "discord:haru", "assistant"), Times.Once);
        _mockLifecycleService.Verify(x => x.Restart("valheim", "api:token", "api"), Times.Once);
    }
}
