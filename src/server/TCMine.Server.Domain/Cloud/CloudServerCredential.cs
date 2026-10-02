using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     A chave com que um servidor de jogo fala com a API da nuvem.
///     Guardamos só o HASH (como no convite): o segredo existe em claro apenas
///     no instante em que é gerado e injetado no container. O prefixo é a parte
///     não secreta — serve para achar a linha sem varrer hashes e para o painel
///     mostrar "qual chave" sem mostrar a chave.
///     Mesmo peso do RconSecret: quem tem a chave grava na nuvem do dono.
/// </summary>
public sealed class CloudServerCredential : Entity
{
    public const int PrefixLength = 12;

    public required Guid GameServerId { get; init; }

    public required Guid VaultId { get; init; }

    public required string KeyPrefix { get; init; }

    public required string KeyHash { get; init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Versão do mod que falou por último com esta chave (diagnóstico no painel).</summary>
    public string? ModVersion { get; private set; }

    /// <summary>
    ///     Mundo que o servidor declarou no hello. Mudar de mundo é normal (mapa
    ///     novo); o que importa para rollback é o checkpoint, não este campo.
    /// </summary>
    public Guid? WorldId { get; private set; }

    public bool IsActive => RevokedAt is null;

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        Touch();
    }

    public void RecordContact(DateTimeOffset now, string? modVersion, Guid? worldId)
    {
        LastSeenAt = now;
        if (modVersion is not null) ModVersion = modVersion.Length > 32 ? modVersion[..32] : modVersion;
        if (worldId is not null) WorldId = worldId;
        Touch();
    }
}
