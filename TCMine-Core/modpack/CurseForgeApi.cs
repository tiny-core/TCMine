using TCMine_Core.Contracts;

namespace TCMine_Core.modpack;

/// <summary>
///     Operações que o <see cref="CurseForgeImporter" /> precisa do CurseForge. O servidor
///     implementa-a com a API direta (key + POST em lote); o cliente sobre o proxy TCMine.
/// </summary>
public interface ICurseForgeApi
{
    /// <summary>Arquivo mais recente de um projeto (o .zip do modpack).</summary>
    Task<CfFileRefDto?> GetLatestFileAsync(long projectId, CancellationToken ct = default);

    /// <summary>Abre o stream de um download (CDN público).</summary>
    Task<Stream> OpenStreamAsync(string url, CancellationToken ct = default);

    /// <summary>Resolve arquivos (nome/url/versão) por id, em lote.</summary>
    Task<IReadOnlyDictionary<long, CfFileRefDto>> GetFilesAsync(
        IReadOnlyCollection<long> fileIds, CancellationToken ct = default);

    /// <summary>Resolve mods (nome/classe) por id, em lote.</summary>
    Task<IReadOnlyDictionary<long, CfModRefDto>> GetModsAsync(
        IReadOnlyCollection<long> modIds, CancellationToken ct = default);
}