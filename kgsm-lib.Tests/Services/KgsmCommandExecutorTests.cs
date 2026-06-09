namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Verifies that <see cref="KgsmCommandExecutor"/> forwards the correct timeout to
/// the process runner: the configured default for the parameterless overload, and
/// the explicit value for the timeout overload. This locks the requirement that a
/// per-operation timeout actually reaches the process layer (the rest of the chain
/// — ProcessRunner enforcing it, InstanceService choosing the per-op value — is
/// covered by ProcessRunnerTests and by inspection while InstanceServiceTests is
/// mid-refactor).
/// </summary>
public class KgsmCommandExecutorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<ILogger<KgsmCommandExecutor>> _logger = new();

    private KgsmCommandExecutor Create(TimeSpan defaultTimeout)
    {
        var options = new KgsmOptions
        {
            KgsmPath = "/opt/kgsm/kgsm.sh",
            Timeouts = new KgsmTimeoutOptions { Default = defaultTimeout }
        };
        return new KgsmCommandExecutor(_processRunner.Object, options, _logger.Object);
    }

    [Fact]
    public void Execute_WithoutExplicitTimeout_ForwardsConfiguredDefault()
    {
        var expected = TimeSpan.FromSeconds(42);
        TimeSpan? captured = null;
        _processRunner
            .Setup(r => r.Execute(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Callback<string, TimeSpan, string[]>((_, timeout, _) => captured = timeout)
            .Returns(new ProcessResult(0, "ok", string.Empty));

        var executor = Create(expected);
        executor.Execute("instances", "info", "terraria");

        Assert.Equal(expected, captured);
    }

    [Fact]
    public void ExecuteForJson_WithoutExplicitTimeout_ForwardsConfiguredDefault()
    {
        var expected = TimeSpan.FromSeconds(7);
        TimeSpan? captured = null;
        _processRunner
            .Setup(r => r.Execute(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Callback<string, TimeSpan, string[]>((_, timeout, _) => captured = timeout)
            .Returns(new ProcessResult(0, "{}", string.Empty));

        var executor = Create(expected);
        executor.ExecuteForJson<Dictionary<string, string>>(["instances", "detailed", "--json"]);

        Assert.Equal(expected, captured);
    }

    [Fact]
    public void Execute_WithExplicitTimeout_ForwardsThatTimeout()
    {
        var explicitTimeout = TimeSpan.FromMinutes(30);
        TimeSpan? captured = null;
        _processRunner
            .Setup(r => r.Execute(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string[]>()))
            .Callback<string, TimeSpan, string[]>((_, timeout, _) => captured = timeout)
            .Returns(new ProcessResult(0, "ok", string.Empty));

        // Default differs from the explicit value, so a pass-through bug would be caught.
        var executor = Create(TimeSpan.FromSeconds(30));
        executor.Execute(explicitTimeout, "install", "factorio");

        Assert.Equal(explicitTimeout, captured);
    }
}
