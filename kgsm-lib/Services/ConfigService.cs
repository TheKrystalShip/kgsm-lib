using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IConfigService interface for managing KGSM configuration.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<ConfigService> _logger;

    /// <summary>
    /// Initializes a new instance of the ConfigService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public ConfigService(IKgsmCommandExecutor commandExecutor, ILogger<ConfigService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("ConfigService initialized");
    }

    /// <inheritdoc/>
    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        KgsmResult result = _commandExecutor.Execute("config", "get", key);

        if (!result.IsSuccess)
            return null;

        string value = result.Stdout.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <inheritdoc/>
    public KgsmResult Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        return _commandExecutor.Execute("config", "set", $"{key}={value}");
    }

    /// <inheritdoc/>
    public Dictionary<string, string> List()
        => _commandExecutor.ExecuteForJson<Dictionary<string, string>>(["config", "list", "--json"]) ?? [];

    /// <inheritdoc/>
    public KgsmResult Reset()
        => _commandExecutor.Execute("config", "reset");

    /// <inheritdoc/>
    public KgsmResult Validate()
        => _commandExecutor.Execute("config", "validate");

    /// <inheritdoc/>
    public KgsmResult Merge()
        => _commandExecutor.Execute("config", "merge");

    /// <inheritdoc/>
    public KgsmResult Rollback(int generation = 0)
    {
        if (generation < 0 || generation > 9)
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "Generation must be between 0 and 9.");

        return _commandExecutor.Execute("config", "rollback", generation.ToString());
    }

    /// <inheritdoc/>
    public KgsmResult Diff(int generation = 0)
    {
        if (generation < 0 || generation > 9)
            throw new ArgumentOutOfRangeException(nameof(generation), generation, "Generation must be between 0 and 9.");

        return _commandExecutor.Execute("config", "diff", generation.ToString());
    }
}
