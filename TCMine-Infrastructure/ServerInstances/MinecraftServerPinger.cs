using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TCMine_Infrastructure.ServerInstances;

/// <summary>Resultado de um ping de status: jogadores online e máximo, além do MOTD.</summary>
public sealed record ServerPing(int Online, int Max, string? Description);

/// <summary>
/// Cliente do <b>Server List Ping</b> (SLP) do Minecraft (protocolo moderno, 1.7+): conecta na porta do
/// jogo, faz o handshake + status request e lê o JSON de status — o mesmo que a lista de servidores do
/// jogo usa para mostrar "X/Y jogadores". É a forma de medir presença sem RCON nem parse de log.
///
/// Tudo é prefixado por <c>VarInt</c> (inteiro de tamanho variável). Falha (servidor fora/ainda subindo)
/// devolve <c>null</c> — o chamador trata como "indisponível".
/// </summary>
public sealed class MinecraftServerPinger
{
    public async Task<ServerPing?> PingAsync(string host, int port, CancellationToken ct = default)
    {
        try
        {
            using var tcp = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3)); // ping curto: servidor não responde rápido = "fora"

            await tcp.ConnectAsync(host, port, timeout.Token);
            await using var stream = tcp.GetStream();

            // Handshake (next state = 1, status)
            var handshake = new MemoryStream();
            WriteVarInt(handshake, 0x00);
            WriteVarInt(handshake, -1); // versão de protocolo: qualquer
            WriteString(handshake, host);
            handshake.WriteByte((byte)(port >> 8));
            handshake.WriteByte((byte)(port & 0xFF));
            WriteVarInt(handshake, 1);
            await WritePacketAsync(stream, handshake.ToArray(), timeout.Token);

            // Status request (vazio)
            await WritePacketAsync(stream, [0x00], timeout.Token);

            // Resposta: <len><packetId 0x00><jsonLen><json>
            _ = await ReadVarIntAsync(stream, timeout.Token); // tamanho total do pacote (ignorado)
            var packetId = await ReadVarIntAsync(stream, timeout.Token);
            if (packetId != 0x00) return null;

            var jsonLen = await ReadVarIntAsync(stream, timeout.Token);
            var buffer = new byte[jsonLen];
            await ReadExactAsync(stream, buffer, timeout.Token);

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buffer));
            var players = doc.RootElement.GetProperty("players");
            var online = players.GetProperty("online").GetInt32();
            var max = players.GetProperty("max").GetInt32();

            string? description = null;
            if (doc.RootElement.TryGetProperty("description", out var desc))
                description = desc.ValueKind == JsonValueKind.String ? desc.GetString() : desc.ToString();

            return new ServerPing(online, max, description);
        }
        catch
        {
            // Conexão recusada / timeout / JSON inesperado → servidor indisponível
            return null;
        }
    }

    // ── Empacotamento ─────────────────────────────────────────────────────────────────────────────

    // Escreve um pacote = VarInt(tamanho do payload) seguido do payload
    private static async Task WritePacketAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        var prefix = new MemoryStream();
        WriteVarInt(prefix, payload.Length);
        await stream.WriteAsync(prefix.ToArray(), ct);
        await stream.WriteAsync(payload, ct);
    }

    private static void WriteString(Stream s, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(s, bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void WriteVarInt(Stream s, int value)
    {
        var v = (uint)value;
        do
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80; // bit de continuação
            s.WriteByte(b);
        } while (v != 0);
    }

    private static async Task<int> ReadVarIntAsync(Stream s, CancellationToken ct)
    {
        var result = 0;
        var shift = 0;
        while (true)
        {
            var b = await ReadByteAsync(s, ct);
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift >= 35) throw new FormatException("VarInt grande demais.");
        }

        return result;
    }

    private static async Task<byte> ReadByteAsync(Stream s, CancellationToken ct)
    {
        var one = new byte[1];
        await ReadExactAsync(s, one, ct);
        return one[0];
    }

    private static async Task ReadExactAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await s.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
