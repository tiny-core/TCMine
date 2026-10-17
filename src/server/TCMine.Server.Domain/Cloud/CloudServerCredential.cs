using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     A chave com que um servidor de jogo fala com a API da nuvem.
///     Guardamos só o HASH (como no convite): o segredo existe em claro apenas
///     no instante em que é gerado e injetado no container. O prefixo é a parte
///     não secreta — serve para achar a linha sem varrer hashes e para o painel
///     mostrar "qual chave" sem mostrar a chave.
///     Mesmo peso do RconSecret: quem tem a chave grava na nuvem do dono.
///     UMA linha por servidor (índice único): cada start reemite a chave NA
///     MESMA linha (<see cref="Reissue" />). Antes era uma linha nova por start,
///     e as revogadas nunca saíam — uma por reinício, para sempre. A linha
///     também guarda o último mundo visto (<see cref="WorldId" />), que é o que
///     a detecção de rollback compara no hello seguinte.
/// </summary>
public sealed class CloudServerCredential : Entity
{
    public const int PrefixLength = 12;

    private const int ModVersionMaxLength = 32;

    // Para o EF. Quem cria usa Issue: os campos da chave só mudam juntos.
    private CloudServerCredential()
    {
    }

    public Guid GameServerId { get; private set; }

    public Guid VaultId { get; private set; }

    public string KeyPrefix { get; private set; } = "";

    public string KeyHash { get; private set; } = "";

    /// <summary>Quando a chave atual foi emitida, se não foi na criação da linha.</summary>
    public DateTimeOffset? ReissuedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Versão do mod que falou por último com esta chave (diagnóstico no painel).</summary>
    public string? ModVersion { get; private set; }

    /// <summary>
    ///     Último mundo que o servidor declarou num hello. Sobrevive à troca de
    ///     chave de propósito: é contra ele que o hello seguinte decide se o
    ///     mundo é o mesmo (e então confere o checkpoint) ou um mapa novo.
    /// </summary>
    public Guid? WorldId { get; private set; }

    public bool IsActive => RevokedAt is null;

    /// <summary>Data da chave em uso — a que o painel mostra.</summary>
    public DateTimeOffset IssuedAt => ReissuedAt ?? CreatedAt;

    public static CloudServerCredential Issue(Guid gameServerId, Guid vaultId, string keyPrefix, string keyHash) =>
        new() { GameServerId = gameServerId, VaultId = vaultId, KeyPrefix = keyPrefix, KeyHash = keyHash };

    /// <summary>
    ///     Chave nova na mesma linha: a anterior deixa de existir no mesmo
    ///     instante (o prefixo dela some do índice), e a nuvem pode ser outra.
    ///     O contato é zerado — era da chave velha —, mas o mundo fica.
    /// </summary>
    public void Reissue(Guid vaultId, string keyPrefix, string keyHash, DateTimeOffset now)
    {
        VaultId = vaultId;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        ReissuedAt = now;
        RevokedAt = null;
        LastSeenAt = null;
        ModVersion = null;
        Touch();
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        Touch();
    }

    public void RecordContact(DateTimeOffset now, string? modVersion, Guid? worldId)
    {
        LastSeenAt = now;
        if (TrimModVersion(modVersion) is { } version) ModVersion = version;
        if (worldId is not null) WorldId = worldId;
        Touch();
    }

    /// <summary>O mod manda o texto que quiser; a coluna guarda até 32 caracteres.</summary>
    public static string? TrimModVersion(string? modVersion) =>
        modVersion is { Length: > ModVersionMaxLength } ? modVersion[..ModVersionMaxLength] : modVersion;
}
