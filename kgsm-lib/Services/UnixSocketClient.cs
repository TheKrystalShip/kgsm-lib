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
        EnsureSocketPathClean();
        
        _logger.LogInformation("Starting Unix socket listener on {SocketPath}", _socketPath);

        using Socket server = CreateAndBindSocket();

        try
        {
            _logger.LogInformation("Unix socket listener started successfully");
            await AcceptConnectionsAsync(server, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Unix socket listener cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Unix socket listener");
            throw;
        }
        finally
        {
            _logger.LogInformation("Unix socket listener stopped");
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
                _logger.LogInformation("Unix socket listener cancelled");
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
