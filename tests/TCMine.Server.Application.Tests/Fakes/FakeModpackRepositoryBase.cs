using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Base para os repositórios fake dos testes: implementa a interface inteira
///     lançando <see cref="NotImplementedException" /> e deixa cada teste
///     sobrescrever só o que usa.
///     Existe porque cada método novo em <see cref="IModpackRepository" /> quebrava
///     oito fakes escritos à mão de uma vez — ruído puro, sem nenhum teste a mais.
///     Um método não sobrescrito que seja chamado ainda explode, que é o
///     comportamento desejado: o teste diz o que espera usar.
/// </summary>
public abstract class FakeModpackRepositoryBase : IModpackRepository
{
    public virtual Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<bool> ExistsFromUpstreamAsync(ModFileOrigin origin, string projectId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyDictionary<Guid, ModpackVersionStats>> GetVersionStatsAsync(
        Guid modpackId, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task CreateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task AddVersionAsync(ModpackVersion version, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task RemoveVersionAsync(Guid versionId, CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>
    ///     No-op em vez de lançar, ao contrário do resto da base.
    ///     Desde que o enfileiramento passa pelo IngestionScheduler, salvar a
    ///     versão virou passo incidental de quase todo caso de uso — e os fakes
    ///     devolvem sempre a mesma instância, então o estado já fica visível ao
    ///     teste sem gravar nada. Obrigar cada fake a sobrescrever isto seria
    ///     ruído puro; quem precisa observar a chamada continua sobrescrevendo.
    /// </summary>
    public virtual Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task RemovePendingAsync(Guid versionId, Guid pendingId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task AddFilesAsync(Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task SaveVersionStateAsync(ModpackVersion version, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<Guid>> ListInterruptedIngestionIdsAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task UpdateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task<PagedResult<ModInventoryEntry>> ListModInventoryAsync(
        ModInventoryQuery query, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task<PagedResult<ModpackFile>> ListVersionModsAsync(
        Guid versionId, string? search, PageRequest page, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlySet<string>> ListReferencedHashesAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>Server packs gravados pelo preenchimento retroativo.</summary>
    public Dictionary<Guid, (string FileId, string? PageUrl)> ServerPacksGravados { get; } = [];

    public virtual Task SetServerPackAsync(
        Guid versionId, string fileId, string? pageUrl, CancellationToken ct)
    {
        ServerPacksGravados[versionId] = (fileId, pageUrl);
        return Task.CompletedTask;
    }
}
