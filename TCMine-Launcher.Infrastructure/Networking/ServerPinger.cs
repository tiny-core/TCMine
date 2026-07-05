using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.Infrastructure.Networking;

/// <summary>Server List Ping do Minecraft (estado online/jogadores/MOTD). Implementa <see cref="IServerPinger"/>.</summary>
public sealed class ServerPinger : IServerPinger
{
    public async Task<ServerPing> PingAsync(string host, int port, int timeoutMs = 4000)
    {
        if (string.IsNullOrWhiteSpace(host)) return ServerPing.Offline;

        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            await using var stream = client.GetStream();

            using (var hs = new MemoryStream())
            {
                WriteVarInt(hs, 0x00);
                WriteVarInt(hs, -1);
                WriteString(hs, host);
                hs.WriteByte((byte)(port >> 8));
                hs.WriteByte((byte)(port & 0xFF));
                WriteVarInt(hs, 1);
                await WritePacketAsync(stream, hs.ToArray(), cts.Token);
            }

            using (var sr = new MemoryStream())
            {
                WriteVarInt(sr, 0x00);
                await WritePacketAsync(stream, sr.ToArray(), cts.Token);
            }

            _ = await ReadVarIntAsync(stream, cts.Token);
            _ = await ReadVarIntAsync(stream, cts.Token);
            var jsonLen = await ReadVarIntAsync(stream, cts.Token);
            var jsonBytes = await ReadExactAsync(stream, jsonLen, cts.Token);
            return ParseStatus(Encoding.UTF8.GetString(jsonBytes));
        }
        catch
        {
            return ServerPing.Offline;
        }
    }

    private static ServerPing ParseStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var online = 0;
            var max = 0;
            if (root.TryGetProperty("players", out var players))
            {
                if (players.TryGetProperty("online", out var o)) online = o.GetInt32();
                if (players.TryGetProperty("max", out var m)) max = m.GetInt32();
            }

            var motd = root.TryGetProperty("description", out var desc) ? StripCodes(ExtractText(desc)) : string.Empty;
            return new ServerPing(true, online, max, motd);
        }
        catch
        {
            return new ServerPing(true, 0, 0, string.Empty);
        }
    }

    private static string ExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;

        var sb = new StringBuilder();
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) sb.Append(t.GetString());
            if (element.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
                foreach (var part in extra.EnumerateArray())
                    sb.Append(ExtractText(part));
        }

        return sb.ToString();
    }

    private static string StripCodes(string s) => Regex.Replace(s, "§.", string.Empty).Trim();

    private static void WriteVarInt(Stream s, int value)
    {
        var v = (uint)value;
        do
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            s.WriteByte(b);
        } while (v != 0);
    }

    private static void WriteString(Stream s, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(s, bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static async Task WritePacketAsync(Stream s, byte[] payload, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        WriteVarInt(ms, payload.Length);
        ms.Write(payload, 0, payload.Length);
        await s.WriteAsync(ms.ToArray(), ct);
    }

    private static async Task<int> ReadVarIntAsync(Stream s, CancellationToken ct)
    {
        var result = 0;
        var shift = 0;
        while (true)
        {
            var buffer = await ReadExactAsync(s, 1, ct);
            var b = buffer[0];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift >= 35) throw new InvalidDataException("VarInt demasiado grande.");
        }

        return result;
    }

    private static async Task<byte[]> ReadExactAsync(Stream s, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await s.ReadAsync(buffer.AsMemory(read, count - read), ct);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }

        return buffer;
    }
}
