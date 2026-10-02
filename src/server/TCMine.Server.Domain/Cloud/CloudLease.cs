using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Qual servidor está com os canais de um jogador numa nuvem — só um por vez
///     (plano §4). É o que impede o mesmo item de ser retirado em dois
///     servidores ao mesmo tempo.
///     A <see cref="Epoch" /> é um "fencing token": aumenta a cada acquire e
///     nunca diminui; a linha nunca é apagada, senão a época voltaria a zero e
///     um lote velho passaria por novo. Um servidor que volta depois de perder o
///     lease manda lotes com a época antiga — e eles vão para a quarentena em
///     vez de sobrescrever o que o outro servidor fez.
///     <see cref="Version" /> é o token de concorrência do EF: aplicar lote é
///     "ler o lease, conferir, gravar"; dois lotes ao mesmo tempo, o segundo
///     perde com DbUpdateConcurrencyException e o servidor de jogo reenvia. É o
///     que substitui o SELECT FOR UPDATE, que o SQLite não tem.
/// </summary>
public sealed class CloudLease : Entity
{
    public required Guid VaultId { get; init; }

    public required string PlayerUuid { get; init; }

    public Guid? HolderServerId { get; private set; }

    public long Epoch { get; private set; }

    /// <summary>Último lote aplicado nesta época (0 = nenhum).</summary>
    public long LastSeq { get; private set; }

    public DateTimeOffset? HeartbeatAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public long Version { get; private set; }

    /// <summary>Livre: ninguém segura, ou quem segurava parou de dar sinal.</summary>
    public bool IsFree(DateTimeOffset now) => HolderServerId is null || ExpiresAt is null || ExpiresAt <= now;

    public bool IsHeldBy(Guid serverId, DateTimeOffset now) => HolderServerId == serverId && !IsFree(now);

    /// <summary>
    ///     Tenta segurar. O MESMO servidor pode pegar de novo (o jogador saiu e
    ///     voltou): ganha época nova — o mod garante que já mandou os lotes da
    ///     época anterior antes de pedir.
    /// </summary>
    public bool TryAcquire(Guid serverId, DateTimeOffset now, TimeSpan ttl)
    {
        if (!IsFree(now) && HolderServerId != serverId) return false;
        HolderServerId = serverId;
        Epoch++;
        LastSeq = 0;
        HeartbeatAt = now;
        ExpiresAt = now + ttl;
        Changed();
        return true;
    }

    /// <summary>Heartbeat: só renova se ainda é do servidor e da época informados.</summary>
    public bool Renew(Guid serverId, long epoch, DateTimeOffset now, TimeSpan ttl)
    {
        if (HolderServerId != serverId || Epoch != epoch) return false;
        HeartbeatAt = now;
        ExpiresAt = now + ttl;
        Changed();
        return true;
    }

    /// <summary>
    ///     O que fazer com um lote. Não muda nada: quem aplica chama
    ///     <see cref="Advance" /> depois de gravar ledger e saldos.
    ///     Um lote da época atual com o holder certo é aceito mesmo com o lease
    ///     vencido — o servidor gravou no diário quando ainda era dono, e enquanto
    ///     ninguém tomou o lease, nada conflita.
    /// </summary>
    public CloudBatchCheck Check(Guid serverId, long epoch, long seq)
    {
        if (epoch < Epoch) return CloudBatchCheck.StaleEpoch;
        if (epoch > Epoch) return CloudBatchCheck.UnknownEpoch;
        if (HolderServerId != serverId) return CloudBatchCheck.NotHolder;
        if (seq <= LastSeq) return CloudBatchCheck.Duplicate;
        return seq == LastSeq + 1 ? CloudBatchCheck.Accept : CloudBatchCheck.Gap;
    }

    public void Advance(long seq)
    {
        if (seq != LastSeq + 1)
            throw new InvalidOperationException($"Sequência fora de ordem: esperado {LastSeq + 1}, veio {seq}.");
        LastSeq = seq;
        Changed();
    }

    /// <summary>
    ///     Libera só se tudo o que o servidor mandou já foi aplicado
    ///     (<paramref name="lastSeq" /> confere). Se faltar lote, continua preso.
    /// </summary>
    public bool Release(Guid serverId, long epoch, long lastSeq)
    {
        if (HolderServerId != serverId || Epoch != epoch || LastSeq != lastSeq) return false;
        HolderServerId = null;
        ExpiresAt = null;
        Changed();
        return true;
    }

    /// <summary>
    ///     O admin libera à força (servidor morto de vez). Lotes que aquele
    ///     servidor ainda tenha no diário chegarão com época velha: quarentena.
    /// </summary>
    public void ForceRelease()
    {
        HolderServerId = null;
        ExpiresAt = null;
        Changed();
    }

    /// <summary>
    ///     O TCMine ficou fora do ar: dá ao dono do lease um TTL inteiro a partir
    ///     de agora. Sem isso a queda do TCMine faria leases expirarem por culpa
    ///     dele, e outro servidor tomaria o canal de quem só não conseguiu avisar.
    /// </summary>
    public void ExtendAfterOutage(DateTimeOffset now, TimeSpan ttl)
    {
        if (HolderServerId is null) return;
        var extended = now + ttl;
        if (ExpiresAt is null || ExpiresAt < extended)
        {
            ExpiresAt = extended;
            Changed();
        }
    }

    private void Changed()
    {
        Version++;
        Touch();
    }
}

public enum CloudBatchCheck
{
    Accept,

    /// <summary>Reenvio de algo já aplicado: responde "duplicado", não aplica de novo.</summary>
    Duplicate,

    /// <summary>Época antiga: o servidor perdeu o lease. Quarentena.</summary>
    StaleEpoch,

    /// <summary>Época que o TCMine nunca emitiu. Quarentena.</summary>
    UnknownEpoch,

    /// <summary>Época atual, mas de outro servidor. Quarentena.</summary>
    NotHolder,

    /// <summary>Faltou um lote antes deste. Quarentena.</summary>
    Gap
}
