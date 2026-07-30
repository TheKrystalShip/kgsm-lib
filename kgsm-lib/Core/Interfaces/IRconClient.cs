using TheKrystalShip.KGSM.Exceptions;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Minimal Source RCON protocol client for querying game servers. Connects via TCP,
/// authenticates with a password, executes commands, and returns raw text responses.
/// Intended for periodic polling (connect → execute → disconnect) rather than
/// persistent connections. AOT-safe — no reflection, no external dependencies.
/// </summary>
public interface IRconClient : IAsyncDisposable
{
    /// <summary>
    /// Connects to the game server's RCON port and authenticates.
    /// </summary>
    /// <param name="host">The game server hostname or IP.</param>
    /// <param name="port">The RCON port.</param>
    /// <param name="password">The RCON password.</param>
    /// <param name="cancellationToken">Cancels the connection attempt.</param>
    /// <exception cref="RconException">Thrown when connection or authentication fails.</exception>
    Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an RCON command and returns the raw text response.
    /// Must be connected first via <see cref="ConnectAsync"/>.
    /// </summary>
    /// <param name="command">The command to execute (e.g. "players", "status").</param>
    /// <param name="cancellationToken">Cancels the command execution.</param>
    /// <returns>The raw text response from the server.</returns>
    /// <exception cref="RconException">Thrown when the command fails or the client is not connected.</exception>
    Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the server. Safe to call even if not connected.
    /// </summary>
    Task DisconnectAsync();
}
