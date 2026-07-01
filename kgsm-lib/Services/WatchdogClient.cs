using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
    // The finite-request client (the readiness/start/stop/status/list/tail verbs are
    // bounded). Carries the configured RequestTimeout.
    private readonly HttpClient _http;

    // A separate client used ONLY for the unbounded console-follow stream. HttpClient's
    // Timeout bounds the WHOLE request including the streamed body read (ResponseHeadersRead
    // only changes when GetAsync returns, not the timeout scope), so a finite Timeout would
    // silently kill a long follow. This one runs at Timeout.InfiniteTimeSpan — the follow
    // ends solely on the caller's CancellationToken, which is the daemon's contract (the
    // stream never self-completes).
    private readonly HttpClient _streamHttp;

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

        _http = new HttpClient(BuildSocketHandler(socketPath), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = options.RequestTimeout,
        };

        // Same UDS transport, but no wall-clock cap — the follow stream is unbounded and
        // ends only when the caller cancels.
        _streamHttp = new HttpClient(BuildSocketHandler(socketPath), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = Timeout.InfiniteTimeSpan,
        };

        _logger.LogDebug("WatchdogClient initialized for control socket {SocketPath}", socketPath);
    }

    /// <summary>
    /// Test-only constructor: drives both the finite and the follow paths through an
    /// injected <see cref="HttpClient"/> (typically wrapping a stub
    /// <see cref="HttpMessageHandler"/>), so the request shapes and response handling can
    /// be unit-tested without a live daemon socket.
    /// </summary>
    internal WatchdogClient(HttpClient httpClient, ILogger<WatchdogClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _logger = logger;
        // Both fields point at the same injected client in tests; the stub handler stands
        // in for the daemon for both the finite and the streaming requests.
        _http = httpClient;
        _streamHttp = httpClient;
    }

    private static SocketsHttpHandler BuildSocketHandler(string socketPath) => new()
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetConsoleTailAsync(string instanceName, int lines, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        using var response = await _http
            .GetAsync($"/console/{Uri.EscapeDataString(instanceName)}?tail={lines}", cancellationToken)
            .ConfigureAwait(false);

        // An unknown / non-native / no-console instance has no console — an honest empty
        // read, not an error (mirrors GetStatusAsync degrading a 404 to null).
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(body))
            return [];

        // The daemon \n-joins the lines with a trailing \n; split and drop that trailing
        // empty element so "no lines" → [] and N lines → exactly N entries.
        var split = body.Split('\n');
        var count = split.Length;
        if (count > 0 && split[count - 1].Length == 0)
            count--;

        if (count == 0)
            return [];

        var result = new string[count];
        Array.Copy(split, result, count);
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<WatchdogPlayer>>?> GetAllPlayersAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var response = await _http.GetAsync("/players", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var raw = await ReadJsonAsync(response, KgsmJsonContext.Default.DictionaryStringWatchdogPlayerArray, cancellationToken)
            .ConfigureAwait(false);

        if (raw is null)
            return null;

        // The daemon serializes as Dictionary<string, WatchdogPlayer[]> — convert to the
        // interface type so consumers get a read-only view.
        var result = new Dictionary<string, IReadOnlyList<WatchdogPlayer>>(StringComparer.Ordinal);
        foreach (var kvp in raw)
            result[kvp.Key] = kvp.Value;
        return result;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> FollowConsoleAsync(
        string instanceName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName, nameof(instanceName));

        // ResponseHeadersRead so we get the response (and can stream the body) without
        // buffering the unbounded chunked body first. The _streamHttp client has an
        // infinite Timeout, so only `cancellationToken` ends this — matching the daemon's
        // contract that the stream never self-completes.
        using var response = await _streamHttp
            .GetAsync(
                $"/console/{Uri.EscapeDataString(instanceName)}/follow",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        // An unknown / non-native / no-console instance answers 404 before the first byte
        // → an empty sequence, not an error.
        if (response.StatusCode == HttpStatusCode.NotFound)
            yield break;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            yield return line;
        }
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
        // In the production ctor _streamHttp is a distinct client; in the test ctor it is
        // the same injected instance, so a double-Dispose is a harmless no-op.
        _streamHttp.Dispose();
        _disposed = true;
    }
}
