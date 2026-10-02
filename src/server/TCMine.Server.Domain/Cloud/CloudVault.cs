using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     A nuvem de itens de um dono (mod <c>tccloud</c>; plano em docs/CLOUD-STORAGE.md).
///     Os servidores do dono ligados a ela compartilham os canais dos jogadores:
///     o que um jogador guarda num servidor, reencontra em outro. Um dono pode
///     ter mais de uma nuvem para separar packs de equilíbrio diferente.
///     O isolamento entre donos começa aqui: um servidor só se liga a uma nuvem
///     do MESMO dono (<see cref="Servers.GameServer.AttachToCloudVault" />).
/// </summary>
public sealed class CloudVault : Entity, IOwnedEntity
{
    public const int NameMaxLength = 64;

    public required string Name { get; set; }

    public Guid OwnerId { get; set; }

    public CloudPolicyMode PolicyMode { get; private set; } = CloudPolicyMode.Blocklist;

    /// <summary>
    ///     Aumenta a cada mudança na política de itens. O servidor de jogo compara
    ///     com a que tem e busca a nova quando difere — sem isso, um item
    ///     bloqueado no painel continuaria entrando até o servidor reiniciar.
    /// </summary>
    public long PolicyVersion { get; private set; } = 1;

    /// <summary>Minutos sem heartbeat até o lease de um jogador poder ser tomado por outro servidor.</summary>
    public int LeaseTtlMinutes { get; private set; } = 30;

    /// <summary>Teto do item codificado; acima disso o servidor de jogo recusa na entrada.</summary>
    public int MaxItemBytes { get; private set; } = 8192;

    public int MaxChannelsPerPlayer { get; private set; } = 5;

    public int MaxTypesPerChannel { get; private set; } = 2000;

    public long MaxTotalPerChannel { get; private set; } = 1_000_000_000L;

    /// <summary>Desligada: os servidores veem a nuvem em somente leitura.</summary>
    public bool IsEnabled { get; private set; } = true;

    public TimeSpan LeaseTtl => TimeSpan.FromMinutes(LeaseTtlMinutes);

    /// <summary>
    ///     Muda os limites de uma vez, validando juntos — um limite isolado fora
    ///     de faixa não pode passar só porque o formulário mandou campo a campo.
    /// </summary>
    public void UpdateLimits(int leaseTtlMinutes, int maxItemBytes, int maxChannelsPerPlayer, int maxTypesPerChannel,
        long maxTotalPerChannel)
    {
        if (leaseTtlMinutes is < 1 or > 24 * 60)
            throw new ArgumentOutOfRangeException(nameof(leaseTtlMinutes), "TTL do lease entre 1 minuto e 24 horas.");
        if (maxItemBytes is < 512 or > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maxItemBytes), "Tamanho do item entre 512 B e 1 MB.");
        if (maxChannelsPerPlayer is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maxChannelsPerPlayer), "Canais por jogador entre 1 e 100.");
        if (maxTypesPerChannel < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTypesPerChannel), "Pelo menos 1 tipo por canal.");
        if (maxTotalPerChannel < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTotalPerChannel), "Pelo menos 1 item por canal.");

        LeaseTtlMinutes = leaseTtlMinutes;
        MaxItemBytes = maxItemBytes;
        MaxChannelsPerPlayer = maxChannelsPerPlayer;
        MaxTypesPerChannel = maxTypesPerChannel;
        MaxTotalPerChannel = maxTotalPerChannel;
        Touch();
    }

    /// <summary>Trocar de modo muda o que entra: conta como mudança de política.</summary>
    public void SetPolicyMode(CloudPolicyMode mode)
    {
        if (PolicyMode == mode) return;
        PolicyMode = mode;
        BumpPolicyVersion();
    }

    /// <summary>Chamado por quem muda regra de item (fatia do painel).</summary>
    public void BumpPolicyVersion()
    {
        PolicyVersion++;
        Touch();
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Touch();
    }
}

public enum CloudPolicyMode
{
    /// <summary>Tudo entra, menos o que as regras bloqueiam.</summary>
    Blocklist,

    /// <summary>Nada entra, menos o que as regras permitem.</summary>
    Allowlist
}
