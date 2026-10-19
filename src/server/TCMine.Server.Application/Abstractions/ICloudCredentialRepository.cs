using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     O que a autenticação do mod precisa, numa linha: a chave e a nuvem a que
///     o servidor dela está ligado AGORA (<paramref name="ServerVaultId" /> nulo =
///     desligado da nuvem, ou o servidor não existe mais).
/// </summary>
public sealed record CloudKeyLookup(
    Guid CredentialId,
    Guid GameServerId,
    Guid VaultId,
    string KeyHash,
    bool IsActive,
    Guid? ServerVaultId);

/// <summary>Chaves com que os servidores de jogo falam com a API da nuvem. Uma por servidor.</summary>
public interface ICloudCredentialRepository
{
    /// <summary>
    ///     Pelo prefixo (parte não secreta da chave): toda requisição do mod
    ///     começa aqui. Chave e servidor numa consulta só — é o caminho mais
    ///     quente da API, e sem cache de propósito (fase 5.5): revogar, trocar
    ///     a chave ou desligar da nuvem tem de valer na requisição seguinte.
    /// </summary>
    Task<CloudKeyLookup?> FindKeyAsync(string prefix, CancellationToken ct);

    Task<CloudServerCredential?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>A linha do servidor, ativa ou revogada; nula se ele nunca recebeu chave.</summary>
    Task<CloudServerCredential?> FindByServerAsync(Guid gameServerId, CancellationToken ct);

    Task AddAsync(CloudServerCredential credential, CancellationToken ct);

    /// <summary>A linha inteira: só para a reemissão, que troca a chave toda de uma vez.</summary>
    Task UpdateAsync(CloudServerCredential credential, CancellationToken ct);

    /// <summary>
    ///     Grava só a data de revogação. Estreito de propósito, como os dois
    ///     abaixo: gravar a linha inteira a partir de uma cópia lida antes podia
    ///     devolver à vida uma chave que outra gravação acabou de trocar.
    /// </summary>
    Task RevokeAsync(Guid gameServerId, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    ///     O contato do hello: data, versão do mod e mundo. Versão ou mundo nulos
    ///     mantêm o que estava.
    /// </summary>
    Task RecordContactAsync(Guid credentialId, DateTimeOffset now, string? modVersion, Guid? worldId,
        CancellationToken ct);

    Task<CloudVault?> GetVaultAsync(Guid vaultId, CancellationToken ct);
}
