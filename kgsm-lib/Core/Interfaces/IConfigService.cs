using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing KGSM configuration.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key to retrieve.</param>
    /// <returns>
    /// The configuration value for the specified key, or null if not found.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
    string? Get(string key);

    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>
    /// Result of the set command execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when key or value is null or whitespace.</exception>
    KgsmResult Set(string key, string value);

    /// <summary>
    /// Lists all configuration values.
    /// </summary>
    /// <returns>
    /// A dictionary of all configuration key-value pairs, or empty if none found.
    /// </returns>
    Dictionary<string, string> List();

    /// <summary>
    /// Resets all configuration values to their defaults.
    /// </summary>
    /// <returns>
    /// Result of the reset command execution.
    /// </returns>
    KgsmResult Reset();

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <returns>
    /// Result of the validate command execution.
    /// IsSuccess will be true if configuration is valid.
    /// </returns>
    KgsmResult Validate();
}
