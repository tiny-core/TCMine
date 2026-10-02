using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Hash do CONTEÚDO de um lote (operações e saldos esperados, em ordem
///     estável). Distingue o reenvio legítimo de um lote já aplicado (mesmo hash:
///     "duplicado") de um lote diferente com o mesmo número (hash diferente:
///     bug ou trapaça, quarentena). As definições de item ficam de fora: são o
///     mesmo item, e reenviar com ou sem elas não muda o lote.
/// </summary>
public static class CloudBatchHash
{
    public static string Of(CloudBatchRequest batch)
    {
        var text = new StringBuilder();
        text.Append(batch.Epoch.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(batch.Seq.ToString(CultureInfo.InvariantCulture)).Append('|');
        foreach (var op in batch.Ops.OrderBy(o => o.ChannelId).ThenBy(o => o.Fingerprint, StringComparer.Ordinal))
            text.Append(op.ChannelId.ToString("N")).Append(':').Append(op.Fingerprint).Append(':')
                .Append(op.Delta.ToString(CultureInfo.InvariantCulture)).Append(';');
        text.Append('|');
        foreach (var e in batch.Expected.OrderBy(x => x.ChannelId).ThenBy(x => x.Fingerprint, StringComparer.Ordinal))
            text.Append(e.ChannelId.ToString("N")).Append(':').Append(e.Fingerprint).Append(':')
                .Append(e.Amount.ToString(CultureInfo.InvariantCulture)).Append(';');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
