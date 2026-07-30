using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Exceptions;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Minimal Source RCON protocol client. Connects via TCP, authenticates with a
/// password, executes commands, and returns raw text responses. AOT-safe — no
/// reflection, no external dependencies. Intended for periodic polling (connect,
/// execute, disconnect) rather than persistent connections.
/// </summary>
/// <remarks>
/// The Source RCON protocol (Valve) uses length-prefixed binary packets over TCP:
/// <list type="bullet">
///   <item>Packet = [Size:4][Id:4][Type:4][Body:N][0x00 0x00]</item>
///   <item>Type 0 = Auth (client→server), auth response id=original for success, id=-1 for failure</item>
///   <item>Type 2 = ExecCommand (client→server)</item>
///   <item>Type 3 = Response (server→client)</item>
/// </list>
/// See https://developer.valvesoftware.com/wiki/Source_RCON_Protocol
/// </remarks>
public sealed class RconClient : IRconClient
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private int _packetId;
    private bool _authenticated;
    private bool _disposed;

    private const int TypeAuth = 0;
    private const int TypeExecCommand = 2;
    private const int TypeResponse = 3;
    private const int AuthResponseId = -1;
    private const int ConnectTimeoutMs = 5000;
    private const int ReadTimeoutMs = 5000;

    /// <inheritdoc/>
    public async Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Disconnect();

        _tcp = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ConnectTimeoutMs);

        try
        {
            await _tcp.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _tcp.Dispose();
            _tcp = null;
            throw new RconException($"Failed to connect to RCON at {host}:{port}", ex);
        }

        _stream = _tcp.GetStream();
        _stream.ReadTimeout = ReadTimeoutMs;

        // Authenticate
        _packetId = 0;
        var authPacket = BuildPacket(_packetId, TypeAuth, password);
        await SendPacketAsync(authPacket, cts.Token).ConfigureAwait(false);

        var response = await ReadPacketAsync(cts.Token).ConfigureAwait(false);

        if (response.Id != _packetId)
        {
            Disconnect();
            throw new RconException("RCON authentication failed (server rejected password)");
        }

        _authenticated = true;
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_authenticated || _stream is null)
            throw new RconException("Not connected. Call ConnectAsync first.");

        _packetId++;
        int id = _packetId;

        var packet = BuildPacket(id, TypeExecCommand, command);
        await SendPacketAsync(packet, cancellationToken).ConfigureAwait(false);

        // Read response(s) — multi-packet responses end with an empty body.
        var sb = new StringBuilder();
        while (true)
        {
            var response = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);

            if (response.Id != id)
                continue; // stale or unrelated packet

            if (response.Body.Length == 0)
                break; // end of multi-packet response

            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(response.Body);
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public Task DisconnectAsync()
    {
        Disconnect();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            Disconnect();
        }
        return ValueTask.CompletedTask;
    }

    private void Disconnect()
    {
        _authenticated = false;
        _stream?.Dispose();
        _stream = null;
        _tcp?.Dispose();
        _tcp = null;
    }

    private async Task SendPacketAsync(byte[] packet, CancellationToken cancellationToken)
    {
        if (_stream is null)
            throw new RconException("Not connected.");

        await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(int Id, string Body)> ReadPacketAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
            throw new RconException("Not connected.");

        // Read 4-byte size prefix
        byte[] sizeBuf = new byte[4];
        await ReadExactAsync(_stream, sizeBuf, cancellationToken).ConfigureAwait(false);
        int size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuf);

        if (size < 10 || size > 4096)
            throw new RconException($"Invalid RCON packet size: {size}");

        // Read remaining payload (size bytes = id + type + body + 2 null terminators)
        byte[] payload = new byte[size];
        await ReadExactAsync(_stream, payload, cancellationToken).ConfigureAwait(false);

        int id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4));
        int type = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));

        // Body is everything between the type field and the two null terminators at the end
        string body = string.Empty;
        if (size > 12) // 4(id) + 4(type) + 2(null) + 2(null) minimum = 12; body = size - 12
        {
            // Strip the two trailing null bytes
            body = Encoding.UTF8.GetString(payload, 8, size - 10);
        }

        return (id, body);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new RconException("Connection closed by server.");
            totalRead += read;
        }
    }

    private static byte[] BuildPacket(int id, int type, string body)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        // Packet: [Size:4][Id:4][Type:4][Body:N][0x00 0x00]
        int size = 4 + 4 + bodyBytes.Length + 2; // id + type + body + null terminators
        byte[] packet = new byte[4 + size]; // size prefix + payload

        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0), size);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), type);
        Buffer.BlockCopy(bodyBytes, 0, packet, 12, bodyBytes.Length);
        packet[^2] = 0x00; // first null terminator
        packet[^1] = 0x00; // second null terminator

        return packet;
    }
}
