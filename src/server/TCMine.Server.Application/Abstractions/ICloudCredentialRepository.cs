using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Abstractions;

/// <summary>Chaves com que os servidores de jogo falam com a API da nuvem.</summary>
public interface ICloudCredentialRepository
{
    /// <summary>Pelo prefixo (parte não secreta da chave): toda requisição do mod começa aqui.</summary>
    Task<CloudServerCredential?> FindByPrefixAsync(string prefix, CancellationToken ct);

    Task<CloudServerCredential?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<CloudServerCredential>> ListActiveByServerAsync(Guid gameServerId, CancellationToken ct);

    Task AddAsync(CloudServerCredential credential, CancellationToken ct);

    Task UpdateAsync(CloudServerCredential credential, CancellationToken ct);

    Task<CloudVault?> GetVaultAsync(Guid vaultId, CancellationToken ct);
}
