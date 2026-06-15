using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// HTTP/1.1-over-unix-socket implementation of <see cref="IWatchdogClient"/>.
/// Mirrors the daemon's transport (Kestrel <c>ListenUnixSocket</c>) from the client
/// side via <see cref="SocketsHttpHandler.ConnectCallback"/> dialing a
/// <see cref="UnixDomainSocketEndPoint"/>. Deserialization runs entirely through the
/// source-generated <see cref="KgsmJsonContext"/> (no reflection), so the client is
/// Native-AOT/trim-safe for consumers like an AOT bot or web BFF.
/// </summary>
public sealed class WatchdogClient : IWatchdogClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WatchdogClient> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes the client against the watchdog control socket.
    /// </summary>
    /// <param name="options">Client options (socket path, request timeout).</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="options"/> or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentException">When the socket path is null, empty, or whitespace.</exception>
    public WatchdogClient(WatchdogClientOptions options, ILogger<WatchdogClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        if (string.IsNullOrWhiteSpace(options.SocketPath))
            throw new ArgumentException("Watchdog socket path cannot be null, empty, or whitespace.", nameof(options));

        _logger = logger;

        var socketPath = options.SocketPath;
        var handler = new SocketsHttpHandler
        {
            // Every HTTP connection is dialed over the unix-domain socket. The Host
            // in the request URI is a placeholder the daemon ignores.
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        _http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = options.RequestTimeout,
        };

        _logger.LogDebug("WatchdogClient initialized for control socket {SocketPath}", socketPath);
    }

    /// <inheritdoc/>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        try
        {
            // Unified ecosystem health probe (/health). Carries readiness: 200 ⇒ in-slice and
            // able to spawn; anything else (503 + reason, or unreachable) ⇒ not ready.
            using var response = await _http.GetAsync("/health", cancellationToken).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (HttpRequestException ex)
        {
            // A down daemon or stale socket is "not ready", not an error to surface.
            _logger.LogDebug(ex, "Watchdog readiness probe failed to connect");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<WatchdogReadyState?> GetReadyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        try
        {
            // /health returns a ReadyState body on both 200 (ready) and 503 (up-but-unable).
            using var response = await _http.GetAsync("/health", cancellationToken).ConfigureAwait(false);
            return await ReadJsonAsync(response, KgsmJsonContext.Default.WatchdogReadyState, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Watchdog /health fetch failed to connect");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<WatchdogActionResult> StartAsync(string instanceName, CancellationToken cancellationToken = default)
        => await PostActionAsync("start", instanceName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<WatchdogActionResult> StopAsync(string instanceName, CancellationToken cancellationToken = default)
        => await PostActionAsync("stop", instanceName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<WatchdogInstanceState?> GetStatusAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        using var response = await _http
            .GetAsync($"/status/{Uri.EscapeDataString(instanceName)}", cancellationToken)
            .ConfigureAwait(false);

        // The daemon does not track this instance.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response, KgsmJsonContext.Default.WatchdogInstanceState, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WatchdogInstanceState>> ListAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var response = await _http.GetAsync("/list", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var states = await ReadJsonAsync(response, KgsmJsonContext.Default.WatchdogInstanceStateArray, cancellationToken)
            .ConfigureAwait(false);
        return states ?? [];
    }

    private async Task<WatchdogActionResult> PostActionAsync(string verb, string instanceName, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        // Both 200 (acted) and 409 (already in the desired state) carry an
        // ActionResult body; only a transport/5xx failure throws.
        using var response = await _http
            .PostAsync($"/{verb}/{Uri.EscapeDataString(instanceName)}", content: null, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Conflict)
            response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync(response, KgsmJsonContext.Default.WatchdogActionResult, cancellationToken)
            .ConfigureAwait(false);

        return result ?? new WatchdogActionResult
        {
            Instance = instanceName,
            Ok = false,
            Message = "Watchdog returned an empty response.",
        };
    }

    private static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _http.Dispose();
        _disposed = true;
    }
}
