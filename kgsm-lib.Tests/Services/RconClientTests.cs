using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TheKrystalShip.KGSM.Exceptions;
using TheKrystalShip.KGSM.Services;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// <see cref="RconClient"/> against a real TCP server speaking the Source RCON wire format.
/// <para>
/// The protocol is only observable on the wire, so these drive an actual socket rather than a mock:
/// a client that composes a packet the server will not accept is indistinguishable, from inside the
/// process, from one that composes it correctly. The failure this guards against — an auth packet
/// sent with the wrong type — is answered by a well-formed rejection, so it surfaces as "the password
/// is wrong" and can be believed for as long as nobody checks the password.
/// </para>
/// </summary>
public class RconClientTests
{
    private const int TypeResponseValue = 0;
    private const int TypeExecCommand = 2;
    private const int TypeAuthResponse = 2;
    private const int TypeAuth = 3;

    [Fact]
    public async Task Authentication_is_sent_as_SERVERDATA_AUTH()
    {
        // Type 3. Type 0 is SERVERDATA_RESPONSE_VALUE — a server→client type, which a server answers
        // with an id of -1, the same rejection a genuinely wrong password earns.
        await using var server = new FakeRconServer("s3cret");
        await using var client = new RconClient();

        await client.ConnectAsync("127.0.0.1", server.Port, "s3cret");

        Assert.Equal(TypeAuth, server.AuthPacketType);
    }

    [Fact]
    public async Task A_wrong_password_is_reported_as_a_rejection()
    {
        await using var server = new FakeRconServer("s3cret");
        await using var client = new RconClient();

        var ex = await Assert.ThrowsAsync<RconException>(
            () => client.ConnectAsync("127.0.0.1", server.Port, "wrong"));

        Assert.Contains("rejected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_empty_response_value_before_the_auth_response_does_not_desync_the_stream()
    {
        // Servers may precede the verdict with an empty SERVERDATA_RESPONSE_VALUE carrying the same
        // id. Taking the first packet as the verdict leaves the real one queued, and every later read
        // returns the previous request's packet.
        await using var server = new FakeRconServer("s3cret") { SendEmptyValueBeforeAuthResponse = true };
        await using var client = new RconClient();

        await client.ConnectAsync("127.0.0.1", server.Port, "s3cret");
        string response = await client.ExecuteCommandAsync("players");

        Assert.Equal("Players connected (1): \n-Juno", response);
    }

    [Fact]
    public async Task A_single_packet_response_returns_without_waiting_for_a_trailer()
    {
        // Project Zomboid answers `players` with one packet of content and nothing after it. Reading
        // until an empty body arrives blocks here until the timeout, discarding an answer already in
        // hand — the whole response is present before the first read completes.
        await using var server = new FakeRconServer("s3cret");
        await using var client = new RconClient();
        await client.ConnectAsync("127.0.0.1", server.Port, "s3cret");

        Task<string> pending = client.ExecuteCommandAsync("players");
        Task finished = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(pending, finished);
        Assert.Equal("Players connected (1): \n-Juno", await pending);
    }

    [Fact]
    public async Task A_split_response_is_reassembled_without_inserted_separators()
    {
        // The parts are byte continuations. A separator between them lands inside whatever token
        // straddled the split — here, in the middle of a player's name.
        await using var server = new FakeRconServer("s3cret")
        {
            ResponseParts = ["Players connected (2): \n-Juno", "vich\n-Ket", "chup"],
        };
        await using var client = new RconClient();
        await client.ConnectAsync("127.0.0.1", server.Port, "s3cret");

        Assert.Equal("Players connected (2): \n-Junovich\n-Ketchup", await client.ExecuteCommandAsync("players"));
    }

    [Fact]
    public async Task A_server_that_never_terminates_the_response_fails_instead_of_hanging()
    {
        // A poller calling this passes its shutdown token, so a read with no deadline of its own
        // stops being a slow poll and becomes a stuck one.
        await using var server = new FakeRconServer("s3cret") { IgnoreSentinel = true };
        await using var client = new RconClient();
        await client.ConnectAsync("127.0.0.1", server.Port, "s3cret");

        var ex = await Assert.ThrowsAsync<RconException>(() => client.ExecuteCommandAsync("players"));

        Assert.Contains("Timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A single-connection Source RCON server. Records what the client actually put on the wire, and
    /// can be told to reproduce the server behaviours that distinguish a correct client from one that
    /// only happens to work against a lenient implementation.
    /// </summary>
    private sealed class FakeRconServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _loop;
        private readonly CancellationTokenSource _cts = new();
        private readonly string _password;

        public FakeRconServer(string password)
        {
            _password = password;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(() => ServeAsync(_cts.Token));
        }

        public int Port { get; }

        /// <summary>The type field of the packet the client authenticated with.</summary>
        public int AuthPacketType { get; private set; } = -1;

        /// <summary>Precede the auth verdict with an empty SERVERDATA_RESPONSE_VALUE.</summary>
        public bool SendEmptyValueBeforeAuthResponse { get; init; }

        /// <summary>Never echo the client's end-of-response sentinel.</summary>
        public bool IgnoreSentinel { get; init; }

        /// <summary>The command response, one entry per packet on the wire.</summary>
        public string[] ResponseParts { get; init; } = ["Players connected (1): \n-Juno"];

        private async Task ServeAsync(CancellationToken token)
        {
            try
            {
                using TcpClient conn = await _listener.AcceptTcpClientAsync(token);
                await using NetworkStream stream = conn.GetStream();

                while (!token.IsCancellationRequested)
                {
                    (int id, int type, string body) = await ReadPacketAsync(stream, token);

                    if (type == TypeAuth || AuthPacketType < 0)
                    {
                        AuthPacketType = type;

                        if (SendEmptyValueBeforeAuthResponse)
                            await WriteAsync(stream, id, TypeResponseValue, "", token);

                        bool ok = type == TypeAuth && body == _password;
                        await WriteAsync(stream, ok ? id : -1, TypeAuthResponse, "", token);

                        if (!ok)
                            return; // a real server drops the connection on a failed auth
                        continue;
                    }

                    if (type == TypeExecCommand)
                    {
                        foreach (string part in ResponseParts)
                            await WriteAsync(stream, id, TypeResponseValue, part, token);
                        continue;
                    }

                    if (type == TypeResponseValue && !IgnoreSentinel)
                        await WriteAsync(stream, id, TypeResponseValue, "", token);
                }
            }
            catch (Exception)
            {
                // the client closing mid-exchange is the normal end of a test
            }
        }

        private static async Task<(int Id, int Type, string Body)> ReadPacketAsync(
            NetworkStream stream, CancellationToken token)
        {
            byte[] sizeBuf = new byte[4];
            await stream.ReadExactlyAsync(sizeBuf, token);
            int size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuf);

            byte[] payload = new byte[size];
            await stream.ReadExactlyAsync(payload, token);

            return (
                BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4)),
                size > 10 ? Encoding.UTF8.GetString(payload, 8, size - 10) : string.Empty);
        }

        private static async Task WriteAsync(
            NetworkStream stream, int id, int type, string body, CancellationToken token)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            int size = 4 + 4 + bodyBytes.Length + 2;
            byte[] packet = new byte[4 + size];

            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0), size);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), id);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), type);
            Buffer.BlockCopy(bodyBytes, 0, packet, 12, bodyBytes.Length);

            await stream.WriteAsync(packet, token);
            await stream.FlushAsync(token);
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try { await _loop; } catch (Exception) { /* cancelled */ }
            _cts.Dispose();
        }
    }
}
