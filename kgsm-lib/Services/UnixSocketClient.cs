using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IUnixSocketClient interface for Unix socket communication.
/// </summary>
public class UnixSocketClient : IUnixSocketClient, IDisposable
{
    private bool _disposed = false;
    private readonly string _socketPath;
    private readonly ILogger<UnixSocketClient> _logger;

    // How often to verify the bound socket file still exists on disk. This bounds the worst-case
    // event-loss window if the file is unlinked out from under the listener.
    private static readonly TimeSpan SocketFileCheckInterval = TimeSpan.FromSeconds(5);
    // Back-off before re-binding after a bind/accept failure (e.g. the runtime directory vanished),
    // so a persistent failure retries steadily instead of spinning.
    private static readonly TimeSpan RebindBackoff = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    public event Func<string, Task>? EventReceived;

    /// <summary>
    /// Initializes a new instance of the UnixSocketClient class with the specified socket path.
    /// </summary>
    /// <param name="options">The options for the Unix socket.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public UnixSocketClient(KgsmOptions options, ILogger<UnixSocketClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _socketPath = options.SocketPath;
        _logger = logger;

        _logger.LogDebug("UnixSocketClient initialized with socket path: {SocketPath}", _socketPath);
    }

    /// <summary>
    /// Finalizer for the UnixSocketClient class.
    /// </summary>
    ~UnixSocketClient()
    {
        Dispose(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources here if needed
            if (File.Exists(_socketPath))
            {
                try
                {
                    File.Delete(_socketPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete socket file: {SocketPath}", _socketPath);
                }
            }
        }

        // Clean up unmanaged resources here if needed
        _disposed = true;
    }

    /// <inheritdoc/>
    public async Task StartListeningAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting Unix socket listener on {SocketPath}", _socketPath);

        // Supervisory loop: bind and accept until cancellation, RE-BINDING in place if the bound
        // socket file is unlinked out from under us. This is the self-heal: when the path is removed
        // after bind (observed on /run tmpfs cleanup), the kernel keeps the LISTEN object alive so
        // AcceptAsync never errors — but the filesystem path is gone, so producers' connect() calls
        // silently ENOENT and events stop arriving. A background watcher detects the vanished file
        // and triggers a re-bind, so the listener recovers without a process restart.
        while (!token.IsCancellationRequested)
        {
            Socket? server = null;
            try
            {
                EnsureSocketPathClean();
                server = CreateAndBindSocket();
                _logger.LogInformation("Unix socket listener started successfully on {SocketPath}", _socketPath);

                // Cancels accept (only) when the socket file disappears; linked to `token` so a real
                // shutdown tears it down too.
                using var rebind = CancellationTokenSource.CreateLinkedTokenSource(token);
                Task watcher = WatchSocketFileAsync(rebind, token);
                try
                {
                    await AcceptConnectionsAsync(server, rebind.Token).ConfigureAwait(false);
                }
                finally
                {
                    if (!rebind.IsCancellationRequested)
                        rebind.Cancel();            // stop the watcher when accept ends for any reason
                    await watcher.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;                              // real shutdown
            }
            catch (Exception ex)
            {
                // A bind/accept failure that is NOT a shutdown — e.g. the whole runtime directory
                // vanished. Back off, then retry binding rather than dying permanently.
                _logger.LogError(ex, "Unix socket listener error — re-binding {SocketPath} after {Backoff}s",
                    _socketPath, RebindBackoff.TotalSeconds);
                try { await Task.Delay(RebindBackoff, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                server?.Dispose();
            }

            if (!token.IsCancellationRequested)
                _logger.LogWarning("Re-binding Unix socket listener on {SocketPath}", _socketPath);
        }

        _logger.LogInformation("Unix socket listener stopped");
    }

    /// <summary>
    /// Watches the bound socket file and requests a re-bind (by cancelling <paramref name="rebind"/>)
    /// if it disappears from disk. Returns when a re-bind is requested or the listener is shutting
    /// down. Existence is the signal: an unlink leaves the kernel LISTEN object intact but the path
    /// dead, which <c>AcceptAsync</c> cannot observe on its own. (A same-path REPLACEMENT by another
    /// process isn't separately detected — rare — but the next re-bind reclaims the path regardless.)
    /// </summary>
    private async Task WatchSocketFileAsync(CancellationTokenSource rebind, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(SocketFileCheckInterval, token).ConfigureAwait(false);
                if (!File.Exists(_socketPath))
                {
                    _logger.LogWarning(
                        "Unix socket file {SocketPath} disappeared from disk — triggering re-bind", _socketPath);
                    rebind.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: the listener is shutting down, or accept already ended and cancelled us.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while watching Unix socket file {SocketPath}", _socketPath);
        }
    }

    /// <summary>
    /// Ensures the socket path is clean by deleting any existing socket file.
    /// </summary>
    /// <exception cref="IOException">Thrown when the socket file cannot be deleted.</exception>
    private void EnsureSocketPathClean()
    {
        if (!File.Exists(_socketPath))
            return;

        _logger.LogDebug("Socket file already exists, deleting: {SocketPath}", _socketPath);
        try
        {
            File.Delete(_socketPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete existing socket file: {SocketPath}", _socketPath);
            throw;
        }
    }

    /// <summary>
    /// Creates and binds a Unix domain socket.
    /// </summary>
    /// <returns>A bound socket ready to accept connections.</returns>
    private Socket CreateAndBindSocket()
    {
        var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var endpoint = new UnixDomainSocketEndPoint(_socketPath);
        server.Bind(endpoint);
        server.Listen(backlog: 5);
        return server;
    }

    /// <summary>
    /// Accepts incoming connections in a loop until cancellation is requested.
    /// </summary>
    /// <param name="server">The server socket to accept connections on.</param>
    /// <param name="token">Cancellation token to stop accepting connections.</param>
    private async Task AcceptConnectionsAsync(Socket server, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for connection on Unix socket");

            Socket? client = null;
            try
            {
                client = await server.AcceptAsync(token).ConfigureAwait(false);
                _logger.LogDebug("Connection accepted on Unix socket");
                await HandleClientConnectionAsync(client, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Either a real shutdown or a re-bind request — the supervisory loop decides which.
                _logger.LogDebug("Accept loop cancelled on {SocketPath}", _socketPath);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept connection on Unix socket");
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    /// <summary>
    /// Handles communication with a connected client.
    /// </summary>
    /// <param name="client">The connected client socket.</param>
    /// <param name="token">Cancellation token to stop reading from the client.</param>
    private async Task HandleClientConnectionAsync(Socket client, CancellationToken token)
    {
        using var networkStream = new NetworkStream(client);
        byte[] buffer = new byte[1024];

        try
        {
            int bytesRead;
            while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                await ProcessIncomingMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Unix socket read operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from Unix socket");
        }
    }

    /// <summary>
    /// Processes an incoming message from the Unix socket.
    /// </summary>
    /// <param name="message">The message to process.</param>
    private async Task ProcessIncomingMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        _logger.LogDebug("Received message from Unix socket: {MessageLength} bytes", message.Length);

        try
        {
            if (EventReceived != null)
            {
                await EventReceived.Invoke(message).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from Unix socket");
        }
    }
}
