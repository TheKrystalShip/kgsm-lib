using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Unit-tests the watchdog console client methods (<c>GetConsoleTailAsync</c> /
/// <c>FollowConsoleAsync</c>) through a stub <see cref="HttpMessageHandler"/> injected
/// via the internal test ctor. The real unix-socket transport is integration territory
/// (like <see cref="UnixSocketClient"/>, deliberately out of the unit suite); these lock
/// the request/response handling that breaks silently without a live daemon: the tail
/// line-splitting + honest-empty 404 degrade, and the follow stream yielding appended
/// lines and ending only on caller cancellation.
/// </summary>
public class WatchdogConsoleClientTests
{
    /// <summary>
    /// A stub handler that returns whatever the supplied factory builds for the request,
    /// capturing the last request URI so a test can assert the wire shape.
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return await responder(request, cancellationToken);
        }
    }

    private static WatchdogClient ClientWith(StubHandler handler)
    {
        var http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost"),
            // A finite timeout, like production's _http — the follow path must NOT be
            // governed by it (it streams via the same injected client here, but the
            // assertion is on cancellation, not the timer).
            Timeout = TimeSpan.FromSeconds(30),
        };
        // The internal test-only ctor (InternalsVisibleTo) points both the finite and the
        // streaming HttpClient at this injected client.
        return new WatchdogClient(http, NullLogger<WatchdogClient>.Instance);
    }

    private static HttpResponseMessage TextResponse(HttpStatusCode status, string body)
    {
        var content = new StringContent(body, Encoding.UTF8, "text/plain");
        return new HttpResponseMessage(status) { Content = content };
    }

    // --- GetConsoleTailAsync ---

    [Fact]
    public async Task GetConsoleTail_SplitsLines_DropsTrailingNewline()
    {
        // The daemon \n-joins lines with a trailing \n.
        var handler = new StubHandler((_, _) =>
            Task.FromResult(TextResponse(HttpStatusCode.OK, "line one\nline two\nline three\n")));
        using var client = ClientWith(handler);

        IReadOnlyList<string> lines = await client.GetConsoleTailAsync("factorio-test", 200);

        Assert.Equal(new[] { "line one", "line two", "line three" }, lines);
        // The request carries the instance name (escaped) and the tail count.
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("/console/factorio-test", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains("tail=200", handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetConsoleTail_EmptyBody_ReturnsEmptyList()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(TextResponse(HttpStatusCode.OK, string.Empty)));
        using var client = ClientWith(handler);

        IReadOnlyList<string> lines = await client.GetConsoleTailAsync("x", 200);

        Assert.Empty(lines);
    }

    [Fact]
    public async Task GetConsoleTail_NotFound_ReturnsEmptyList_NeverThrows()
    {
        // An unknown / non-native / no-console instance answers 404 — an honest "no
        // console", degraded to an empty list (mirrors GetStatusAsync's 404→null).
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = ClientWith(handler);

        IReadOnlyList<string> lines = await client.GetConsoleTailAsync("ghost", 200);

        Assert.Empty(lines);
    }

    [Fact]
    public async Task GetConsoleTail_EscapesInstanceName()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(TextResponse(HttpStatusCode.OK, string.Empty)));
        using var client = ClientWith(handler);

        await client.GetConsoleTailAsync("a b/c", 10);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("/console/a%20b%2Fc", handler.LastRequestUri!.AbsolutePath);
    }

    // --- GetConsoleWindowAsync (the cursor that makes reading further back exact) ---

    private static HttpResponseMessage WindowResponse(string body, long start, long end)
    {
        var response = TextResponse(HttpStatusCode.OK, body);
        response.Headers.Add("X-Console-Start", start.ToString());
        response.Headers.Add("X-Console-End", end.ToString());
        return response;
    }

    [Fact]
    public async Task GetConsoleWindow_CarriesTheByteRangeBack()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(WindowResponse("a\nb\n", 40, 44)));
        using var client = ClientWith(handler);

        var window = await client.GetConsoleWindowAsync("factorio-test", 2, run: 0, endOffset: -1);

        Assert.Equal(new[] { "a", "b" }, window.Lines);
        Assert.Equal(40, window.Start);
        Assert.Equal(44, window.End);
        Assert.True(window.HasEarlier);
    }

    [Fact]
    public async Task GetConsoleWindow_AtTheStartOfTheRun_HasNothingEarlier()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(WindowResponse("first\n", 0, 6)));
        using var client = ClientWith(handler);

        var window = await client.GetConsoleWindowAsync("factorio-test", 200, run: 0, endOffset: -1);

        Assert.False(window.HasEarlier);
    }

    [Fact]
    public async Task GetConsoleWindow_SendsTheCursorOnlyWhenAskedToPageBack()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(WindowResponse(string.Empty, 0, 0)));
        using var client = ClientWith(handler);

        await client.GetConsoleWindowAsync("x", 200, run: 0, endOffset: -1);
        Assert.DoesNotContain("end=", handler.LastRequestUri!.Query);

        await client.GetConsoleWindowAsync("x", 200, run: 0, endOffset: 1234);
        Assert.Contains("end=1234", handler.LastRequestUri!.Query);
    }

    [Fact]
    public async Task GetConsoleWindow_WithoutTheHeaders_ReadsAsTheStartOfTheRun()
    {
        // A daemon too old to report the range. Offering no way further back is the honest
        // degrade; a fabricated cursor would re-serve the same lines forever.
        var handler = new StubHandler((_, _) => Task.FromResult(TextResponse(HttpStatusCode.OK, "a\nb\n")));
        using var client = ClientWith(handler);

        var window = await client.GetConsoleWindowAsync("x", 2, run: 0, endOffset: -1);

        Assert.Equal(new[] { "a", "b" }, window.Lines);
        Assert.False(window.HasEarlier);
    }

    [Fact]
    public async Task GetConsoleWindow_NotFound_IsAnEmptyWindow_NeverThrows()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(TextResponse(HttpStatusCode.NotFound, string.Empty)));
        using var client = ClientWith(handler);

        var window = await client.GetConsoleWindowAsync("ghost", 200, run: 0, endOffset: -1);

        Assert.Empty(window.Lines);
        Assert.False(window.HasEarlier);
    }

    // --- OpenConsoleDownloadAsync (the whole run, as a stream) ---

    [Fact]
    public async Task OpenConsoleDownload_HandsBackTheStreamAndItsLength()
    {
        var payload = Encoding.UTF8.GetBytes("line-1\nline-2\n");
        var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        });
        using var client = ClientWith(handler);

        using var download = await client.OpenConsoleDownloadAsync("factorio-test", run: 0);

        Assert.NotNull(download);
        Assert.Equal(payload.Length, download!.Length);
        using var reader = new StreamReader(download.Content);
        Assert.Equal("line-1\nline-2\n", await reader.ReadToEndAsync());
        Assert.Equal("/console/factorio-test/download", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task OpenConsoleDownload_NoConsole_IsNull_NotAnEmptyStream()
    {
        // "There is no console here" and "the console is empty" are different facts, and a caller
        // reporting them the same way tells somebody their log is empty when it was never readable.
        var handler = new StubHandler((_, _) =>
            Task.FromResult(TextResponse(HttpStatusCode.NotFound, string.Empty)));
        using var client = ClientWith(handler);

        Assert.Null(await client.OpenConsoleDownloadAsync("ghost", run: 0));
    }

    [Fact]
    public async Task OpenConsoleDownload_OfAnInstanceThatNeverPrinted_IsAnEmptyDownload()
    {
        var handler = new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentLength = 0;
            return Task.FromResult(response);
        });
        using var client = ClientWith(handler);

        using var download = await client.OpenConsoleDownloadAsync("quiet", run: 0);

        Assert.NotNull(download);
        Assert.Equal(0, download!.Length);
    }

    // --- FollowConsoleAsync ---

    [Fact]
    public async Task FollowConsole_YieldsAppendedLines()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(TextResponse(HttpStatusCode.OK, "first\nsecond\nthird\n")));
        using var client = ClientWith(handler);

        var received = new List<string>();
        await foreach (var line in client.FollowConsoleAsync("factorio-test"))
            received.Add(line);

        Assert.Equal(new[] { "first", "second", "third" }, received);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("/console/factorio-test/follow", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task FollowConsole_NotFound_YieldsEmptySequence()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = ClientWith(handler);

        var received = new List<string>();
        await foreach (var line in client.FollowConsoleAsync("ghost"))
            received.Add(line);

        Assert.Empty(received);
    }

    [Fact]
    public async Task FollowConsole_Cancellation_StopsEnumeration()
    {
        // A stream that emits one line then BLOCKS on the token (never self-completing) —
        // the production contract. Without an explicit block the stub would complete on
        // its own and "cancellation stops enumeration" would be vacuously true. We assert
        // the cancel actually surfaces as an OperationCanceledException out of the
        // enumerator, proving the token (not a self-completing stream) ended it.
        var firstLineEmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new StubHandler((_, ct) =>
        {
            var stream = new BlockingLineStream("only-line\n", firstLineEmitted, ct);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream),
            };
            return Task.FromResult(response);
        });
        using var client = ClientWith(handler);
        using var cts = new CancellationTokenSource();

        var received = new List<string>();
        var enumeration = Task.Run(async () =>
        {
            await foreach (var line in client.FollowConsoleAsync("factorio-test", cts.Token))
                received.Add(line);
        });

        // Wait until the stream has handed over the first line (so we are genuinely mid-stream).
        await firstLineEmitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumeration);
        Assert.Equal(new[] { "only-line" }, received);
    }

    /// <summary>
    /// A read-only stream that yields a single chunk of bytes once, signals it has done so,
    /// then blocks every subsequent read until the cancellation token fires (then throws
    /// <see cref="OperationCanceledException"/>) — modelling the daemon's unbounded
    /// never-self-completing follow stream.
    /// </summary>
    private sealed class BlockingLineStream(string firstChunk, TaskCompletionSource emitted, CancellationToken streamCt) : Stream
    {
        private byte[] _pending = Encoding.UTF8.GetBytes(firstChunk);
        private int _offset;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_offset < _pending.Length)
            {
                int n = Math.Min(buffer.Length, _pending.Length - _offset);
                _pending.AsSpan(_offset, n).CopyTo(buffer.Span);
                _offset += n;
                if (_offset >= _pending.Length)
                    emitted.TrySetResult();
                return n;
            }

            // No more data — block until cancelled (the stream never ends on its own).
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, streamCt);
            await Task.Delay(Timeout.Infinite, linked.Token).ConfigureAwait(false);
            return 0; // unreachable — Delay throws on cancellation.
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
