using System.Text.Json;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Provides a generic execution interface for KGSM commands with built-in validation,
/// logging, and result parsing.
/// </summary>
public interface IKgsmCommandExecutor
{
    /// <summary>
    /// Executes a KGSM command and deserializes the JSON output to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON output to.</typeparam>
    /// <param name="args">The command arguments to pass to KGSM.</param>
    /// <param name="configureOptions">Optional action to customize JSON serializer options.</param>
    /// <param name="defaultValue">The default value to return if execution or deserialization fails.</param>
    /// <returns>The deserialized result, or the default value if the operation fails.</returns>
    T? ExecuteForJson<T>(
        string[] args,
        Action<JsonSerializerOptions>? configureOptions = null,
        T? defaultValue = default);

    /// <summary>
    /// Executes a KGSM command asynchronously and deserializes the JSON output to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON output to.</typeparam>
    /// <param name="args">The command arguments to pass to KGSM.</param>
    /// <param name="configureOptions">Optional action to customize JSON serializer options.</param>
    /// <param name="defaultValue">The default value to return if execution or deserialization fails.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The deserialized result, or the default value if the operation fails.</returns>
    Task<T?> ExecuteForJsonAsync<T>(
        string[] args,
        Action<JsonSerializerOptions>? configureOptions = null,
        T? defaultValue = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a KGSM command and returns the raw result wrapped in a KgsmResult.
    /// </summary>
    /// <param name="args">The command arguments to pass to KGSM.</param>
    /// <returns>A KgsmResult containing the command output and exit code.</returns>
    KgsmResult Execute(params string[] args);

    /// <summary>
    /// Executes a KGSM command asynchronously and returns the raw result wrapped in a KgsmResult.
    /// </summary>
    /// <param name="args">The command arguments to pass to KGSM.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A KgsmResult containing the command output and exit code.</returns>
    Task<KgsmResult> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
