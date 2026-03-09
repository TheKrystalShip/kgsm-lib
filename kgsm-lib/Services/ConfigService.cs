using System.Text.Json;
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

        _logger.LogDebug("Getting configuration value for key: {Key}", key);

        KgsmResult result = _commandExecutor.Execute("config", "get", key);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to get configuration value for key {Key}: {Error}", key, result.Stderr);
            return null;
        }

        string value = result.Stdout.Trim();
        _logger.LogDebug("Retrieved configuration value for key {Key}: {Value}", key, value);

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <inheritdoc/>
    public KgsmResult Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        _logger.LogDebug("Setting configuration value for key {Key} to {Value}", key, value);

        KgsmResult result = _commandExecutor.Execute("config", "set", $"{key}={value}");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully set configuration value for key {Key}", key);
        }
        else
        {
            _logger.LogError("Failed to set configuration value for key {Key}: {Error}", key, result.Stderr);
        }

        return result;
    }

    /// <inheritdoc/>
    public Dictionary<string, string> List()
    {
        _logger.LogDebug("Listing all configuration values");

        Dictionary<string, string>? config = _commandExecutor
            .ExecuteForJson<Dictionary<string, string>>(["config", "list", "--json"]);

        if (config == null)
        {
            _logger.LogWarning("No configuration values found");
            return new Dictionary<string, string>();
        }

        _logger.LogDebug("Found {Count} configuration values", config.Count);
        return config;
    }

    /// <inheritdoc/>
    public KgsmResult Reset()
    {
        _logger.LogInformation("Resetting configuration to defaults");

        KgsmResult result = _commandExecutor.Execute("config", "reset");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Successfully reset configuration to defaults");
        }
        else
        {
            _logger.LogError("Failed to reset configuration: {Error}", result.Stderr);
        }

        return result;
    }

    /// <inheritdoc/>
    public KgsmResult Validate()
    {
        _logger.LogDebug("Validating configuration");

        KgsmResult result = _commandExecutor.Execute("config", "validate");

        if (result.IsSuccess)
        {
            _logger.LogInformation("Configuration is valid");
        }
        else
        {
            _logger.LogWarning("Configuration validation failed: {Error}", result.Stderr);
        }

        return result;
    }
}
