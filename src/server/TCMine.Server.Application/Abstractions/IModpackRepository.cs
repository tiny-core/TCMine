using TCMine.Contracts.Modpacks;
﻿using TCMine.Server.Application.Common;
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

    /// <summary>Já existe modpack importado deste pack externo?</summary>
    Task<bool> ExistsFromUpstreamAsync(ModFileOrigin origin, string projectId, CancellationToken ct);

    /// <summary>
    ///     Contagens por versão, agregadas no banco.
    ///     Existe para a tela de detalhe não precisar materializar milhares de
    ///     linhas de arquivo só para exibir "471 mods" — num pack importado isso
    ///     é a diferença entre a página abrir na hora e parecer travada.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ModpackVersionStats>> GetVersionStatsAsync(
        Guid modpackId, CancellationToken ct);

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

    /// <summary>
    ///     Insere arquivos numa versão existente, sem reanexar o grafo.
    ///     Existe para a ingestão gravar em lotes enquanto baixa: com
    ///     <see cref="UpdateVersionAsync" />, cada save custaria reanexar as
    ///     milhares de linhas já presentes — O(n) por lote, O(n²) na corrida.
    /// </summary>
    Task AddFilesAsync(Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct);

    /// <summary>
    ///     Grava só a versão (estado, timestamps) e suas pendências, deixando os
    ///     arquivos em paz. Par de <see cref="AddFilesAsync" />: quem já gravou os
    ///     arquivos pontualmente não pode pagar por um UPDATE em cada um deles no
    ///     fecho da ingestão.
    /// </summary>
    Task SaveVersionStateAsync(ModpackVersion version, CancellationToken ct);

    /// <summary>
    ///     Apaga uma pendência resolvida. Como no caso dos arquivos, remover da
    ///     coleção de um grafo destacado não apaga a linha sozinho.
    /// </summary>
    Task RemovePendingAsync(Guid versionId, Guid pendingId, CancellationToken ct);

    /// <summary>
    ///     Grava só os dois campos do server pack.
    ///     Método estreito de propósito: o preenchimento retroativo roda sobre
    ///     versões já publicadas, e passar por UpdateVersionAsync reanexaria o
    ///     grafo inteiro — milhares de arquivos remarcados para escrever dois
    ///     campos, numa versão que é imutável em todo o resto.
    /// </summary>
    Task SetServerPackAsync(Guid versionId, string fileId, string? pageUrl, CancellationToken ct);

    /// <summary>
    ///     Grava só o lado de um arquivo. Estreito pelo mesmo motivo do
    ///     <see cref="SetServerPackAsync" />: um enum de uma linha não justifica
    ///     reanexar milhares de arquivos.
    /// </summary>
    Task SetFileSideAsync(Guid versionId, Guid fileId, FileSide side, CancellationToken ct);

    /// <summary>
    ///     Ingestões que ficaram pela metade quando o processo caiu.
    ///     São duas situações, e as duas contam: a versão presa em Resolving (o
    ///     worker morreu no meio) e o rascunho com mods ainda marcados como
    ///     Queued (caiu antes de o worker sequer pegar o job). Sem a segunda, um
    ///     pedido feito segundos antes de um deploy sumiria sem deixar vestígio.
    ///     Devolve só os ids: quem recupera precisa do grafo completo (arquivos e
    ///     pendências) e o carrega pelo GetVersionAsync, que já sabe montá-lo.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListInterruptedIngestionIdsAsync(CancellationToken ct);

    Task UpdateAsync(Modpack modpack, CancellationToken ct);

    /// <summary>
    ///     Inventário de mods do sistema inteiro, agregado e paginado no banco.
    ///     Uma linha por mod (não por arquivo): o mesmo mod aparece em várias
    ///     versões de vários modpacks, e listar arquivo a arquivo daria dezenas de
    ///     milhares de linhas dizendo a mesma coisa. Filtro e ordenação vão em SQL
    ///     junto com o recorte — filtrar depois de trazer tudo derrotaria o
    ///     propósito da página.
    /// </summary>
    Task<PagedResult<ModInventoryEntry>> ListModInventoryAsync(ModInventoryQuery query, CancellationToken ct);

    /// <summary>
    ///     Arquivos de UMA versão, paginados, na metade pedida pelo escopo.
    ///     Overrides ficam de fora — têm aba própria, e num pack importado são
    ///     milhares.
    /// </summary>
    Task<PagedResult<ModpackFile>> ListVersionFilesAsync(
        Guid versionId, VersionFileScope scope, string? search, PageRequest page, CancellationToken ct);

    /// <summary>
    ///     Todo hash referenciado por alguma coisa: arquivo de versão (inclusive
    ///     de versões arquivadas) ou capa de modpack.
    ///     É a lista de "não pode apagar". Deliberadamente NÃO filtra por estado:
    ///     uma versão arquivada continua instalada na máquina de quem já a
    ///     baixou, e apagar os bytes dela quebraria essas instalações.
    /// </summary>
    Task<IReadOnlySet<string>> ListReferencedHashesAsync(CancellationToken ct);
}

/// <summary>Filtros do inventário. Todos opcionais; combinam em AND.</summary>
public sealed record ModInventoryQuery(
    PageRequest Page,
    string? Search = null,
    ModFileOrigin? Origin = null,
    bool OnlyOrphans = false);

/// <summary>
///     Um mod visto de cima: onde está e se ainda importa.
///     <see cref="IsOrphan" /> é a pergunta prática — "posso parar de me
///     preocupar com este mod?". Sim quando ele só sobrevive em versões
///     arquivadas: ninguém novo vai instalá-lo, e os blobs dele são candidatos a
///     coleta de lixo.
/// </summary>
public sealed record ModInventoryEntry(
    string ProjectSlug,
    string DisplayName,
    ModFileOrigin Origin,
    string? IconUrl,
    long SizeBytes,
    IReadOnlyList<string> Modpacks,
    int ActiveReferences,
    int TotalReferences)
{
    public bool IsOrphan => ActiveReferences is 0;
}

/// <summary>Contagens de uma versão, sem carregar os arquivos.</summary>
public sealed record ModpackVersionStats(int ModCount, int OverrideCount, long TotalSizeBytes)
{
    public int TotalCount => ModCount + OverrideCount;

    public static ModpackVersionStats Empty { get; } = new(0, 0, 0);
}

/// <summary>
///     Qual metade dos arquivos de uma versão listar.
///     São complementares: o que não é recurso é mod. A separação existe porque
///     as duas coisas se gerenciam de formas diferentes — mod tem lado, origem e
///     atualização; shaderpack e resource pack são arquivos que o admin sobe.
/// </summary>
public enum VersionFileScope
{
    Mods,
    Assets
}
