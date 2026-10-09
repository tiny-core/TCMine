using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Decide se um lote pode ser aplicado e o que ele grava — função pura, sem
///     banco, para cada regra ter teste próprio. O lease (época/seq) já foi
///     conferido antes; aqui é o conteúdo: canais do jogador, itens conhecidos,
///     saldo nunca negativo, cotas da nuvem e saldo esperado batendo com o que o
///     TCMine calcula. Qualquer falha recusa o lote INTEIRO: aplicar metade
///     deixaria o servidor de jogo e o banco discordando sem ninguém saber.
/// </summary>
public static class CloudBatchDecision
{
    /// <param name="ownerApproval">
    ///     O dono está aplicando um lote da quarentena: canal congelado, cota e
    ///     saldo esperado pelo mod não barram (são exatamente o que ele está
    ///     decidindo passar por cima). Saldo negativo, canal e item desconhecidos
    ///     continuam barrando — esses não têm como ser aplicados.
    /// </param>
    public static Outcome Decide(CloudBatchRequest batch, State state, bool ownerApproval = false)
    {
        var touched = batch.Ops.Select(o => o.ChannelId).Distinct().ToArray();

        if (batch.Ops.Count == 0 || batch.Ops.Count > CloudProtocol.MaxOpsPerBatch
                                 || (batch.Definitions?.Count ?? 0) > CloudProtocol.MaxOpsPerBatch)
            return Reject(CloudQuarantineReason.PayloadMismatch, "Lote vazio ou grande demais.", touched);

        if (batch.Ops.Any(o => o.Delta == 0)
            || batch.Ops.GroupBy(o => (o.ChannelId, o.Fingerprint)).Any(g => g.Count() > 1))
            return Reject(CloudQuarantineReason.PayloadMismatch, "Operação zerada ou item repetido no lote.", touched);

        // Definições novas: validadas aqui porque viram linha permanente no banco.
        var definitions = new Dictionary<string, CloudItemDto>(StringComparer.Ordinal);
        foreach (var d in batch.Definitions ?? [])
        {
            var problem = ValidateDefinition(d, state.MaxItemBytes);
            if (problem is not null)
                return Reject(CloudQuarantineReason.UnknownItem, problem, touched);
            definitions.TryAdd(d.Fingerprint, d);
        }

        var changes = new List<Change>(batch.Ops.Count);
        var newItems = new Dictionary<string, CloudItemDto>(StringComparer.Ordinal);
        foreach (var op in batch.Ops)
        {
            if (!state.Channels.TryGetValue(op.ChannelId, out var channel))
                return Reject(CloudQuarantineReason.UnknownChannel, $"Canal {op.ChannelId} não é deste jogador.",
                    touched);
            if (channel.IsFrozen && !ownerApproval)
                return Reject(CloudQuarantineReason.FrozenChannel, $"Canal {channel.Name} está congelado.", touched);

            var known = state.ItemIds.ContainsKey(op.Fingerprint);
            if (!known)
            {
                // Item que o banco não conhece só pode ENTRAR, e com a definição junto.
                if (op.Delta < 0 || !definitions.TryGetValue(op.Fingerprint, out var def))
                    return Reject(CloudQuarantineReason.UnknownItem, $"Item {op.Fingerprint} sem definição.", touched);
                newItems.TryAdd(op.Fingerprint, def);
            }

            var before = state.Balances.GetValueOrDefault((op.ChannelId, op.Fingerprint));
            var after = before + op.Delta;
            if (after < 0)
            {
                return Reject(CloudQuarantineReason.NegativeBalance,
                    $"Saldo de {op.Fingerprint} ficaria {after} no canal {channel.Name}.", touched);
            }

            changes.Add(new Change(op.ChannelId, op.Fingerprint, op.Delta, after));
        }

        if (ownerApproval)
            return new Accepted(changes, newItems.Values.ToList());

        var quotaProblem = CheckQuota(changes, state);
        if (quotaProblem is not null)
            return Reject(CloudQuarantineReason.QuotaExceeded, quotaProblem, touched);

        // O servidor de jogo diz quanto ACHA que fica; se o TCMine calcula outra
        // coisa, os dois lados divergiram — parar aqui é o que evita espalhar.
        var expected = batch.Expected.ToDictionary(e => (e.ChannelId, e.Fingerprint), e => e.Amount);
        foreach (var c in changes)
        {
            if (!expected.TryGetValue((c.ChannelId, c.Fingerprint), out var amount) || amount != c.After)
            {
                return Reject(CloudQuarantineReason.Divergence,
                    $"Saldo esperado de {c.Fingerprint} não confere (TCMine: {c.After}).", touched);
            }
        }

        return new Accepted(changes, newItems.Values.ToList());
    }

    private static string? CheckQuota(IReadOnlyList<Change> changes, State state)
    {
        foreach (var channel in changes.Where(c => c.Delta > 0).Select(c => c.ChannelId).Distinct())
        {
            var after = state.Balances
                .Where(b => b.Key.Channel == channel)
                .ToDictionary(b => b.Key.Fingerprint, b => b.Value, StringComparer.Ordinal);
            foreach (var c in changes.Where(c => c.ChannelId == channel))
                after[c.Fingerprint] = c.After;

            var types = after.Count(kv => kv.Value > 0);
            var total = after.Values.Where(v => v > 0).Sum();
            if (types > state.MaxTypesPerChannel)
                return $"Canal com {types} tipos de item (limite {state.MaxTypesPerChannel}).";
            if (total > state.MaxTotalPerChannel)
                return $"Canal com {total} itens (limite {state.MaxTotalPerChannel}).";
        }

        return null;
    }

    private static string? ValidateDefinition(CloudItemDto d, int maxItemBytes)
    {
        if (d.Fingerprint is not { Length: CloudItemType.FingerprintLength } fp || !fp.All(Uri.IsHexDigit))
            return "Impressão digital inválida.";
        if (string.IsNullOrWhiteSpace(d.ItemId) || d.ItemId.Length > 256 || !d.ItemId.Contains(':'))
            return $"Id de item inválido em {fp}.";
        if (d.DisplayName is null || d.DisplayName.Length > 128)
            return $"Nome de item inválido em {fp}.";
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(d.Encoded);
        }
        catch (FormatException)
        {
            return $"Bytes do item {fp} não são base64.";
        }

        return bytes.Length is 0 || bytes.Length > maxItemBytes ? $"Item {fp} com {bytes.Length} bytes." : null;
    }

    private static Rejected Reject(CloudQuarantineReason reason, string detail, IReadOnlyCollection<Guid> channels) =>
        new(reason, detail, channels);

    /// <summary>Estado atual relevante para o lote.</summary>
    /// <param name="Channels">canais do jogador nesta nuvem</param>
    /// <param name="ItemIds">impressão digital → id, dos itens já existentes no banco</param>
    /// <param name="Balances">saldo atual por (canal, impressão digital), para todos os canais do jogador</param>
    public sealed record State(
        IReadOnlyDictionary<Guid, CloudChannel> Channels,
        IReadOnlyDictionary<string, Guid> ItemIds,
        IReadOnlyDictionary<(Guid Channel, string Fingerprint), long> Balances,
        int MaxTypesPerChannel,
        long MaxTotalPerChannel,
        int MaxItemBytes);

    public sealed record Change(Guid ChannelId, string Fingerprint, long Delta, long After);

    /// <summary>Resultado: <see cref="Accepted" /> ou <see cref="Rejected" /> (≈ união discriminada).</summary>
    public abstract record Outcome;

    /// <summary>Lote aceito: o que gravar. <c>NewItems</c> = definições de itens que o banco ainda não tem.</summary>
    public sealed record Accepted(IReadOnlyList<Change> Changes, IReadOnlyList<CloudItemDto> NewItems) : Outcome;

    public sealed record Rejected(CloudQuarantineReason Reason, string Detail, IReadOnlyCollection<Guid> Channels)
        : Outcome;
}
