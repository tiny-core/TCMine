using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Acesso à persistência de modpacks.
///     Cada método é atômico: abre um contexto, faz o trabalho, grava e fecha.
///     Não há Add + SaveChanges separado porque, no Blazor Server, um contexto
///     compartilhado entre operações acumula estado rastreado de toda a sessão e
///     acaba colidindo. Contexto curto por operação é o padrão correto aqui.
/// </summary>
public interface IModpackRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

    Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct);

    Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct);

    Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct);

    Task RemoveAsync(Guid id, CancellationToken ct);

    /// <summary>Persiste um modpack novo.</summary>
    Task CreateAsync(Modpack modpack, CancellationToken ct);

    /// <summary>Anexa uma versão a um modpack existente.</summary>
    Task AddVersionAsync(ModpackVersion version, CancellationToken ct);

    Task RemoveVersionAsync(Guid versionId, CancellationToken ct);

    /// <summary>
    ///     Grava alterações numa versão já existente e seus arquivos.
    ///     Recebe a entidade inteira e reconcilia: é o que permite ao caso de uso
    ///     carregar, mexer no domínio (adicionar arquivo, mudar estado) e mandar
    ///     gravar, sem se preocupar com rastreamento.
    /// </summary>
    Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct);

    /// <summary>
    ///     Traz o modpack com versões e a contagem de arquivos de cada uma, numa
    ///     consulta só. Evita o N+1 de carregar cada versão separadamente para a
    ///     tela de detalhe.
    /// </summary>
    Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct);

    Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct);

    Task UpdateAsync(Modpack modpack, CancellationToken ct);
}
