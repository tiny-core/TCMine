using TCMine_Domain.Modpack;

namespace TCMine_Application.Contracts;

/// <summary>Resultado de uma mesclagem de listas de mods.</summary>
/// <typeparam name="T">Tipo do mod (ModEntry no cliente, ModEntryEntity no servidor).</typeparam>
public sealed record MergeResultDto<T>(List<T> Items, int Added, int Updated);

/// <summary>
/// Representa um mod em um modpack, incluindo detalhes como identificadores, nome, versão e URL de download.
/// </summary>
public record ModDto(
    long ModId,
    long FileId,
    string Name,
    string FileName,
    string DownloadUrl,
    string Target,
    string? Version = null);

/// <summary>
/// Representa um manifesto detalhado de um modpack, incluindo metadados, configuração,
/// detalhes de mods e informações do servidor.
/// </summary>
public record ModpackManifestDto(
    Guid Id,
    string Name,
    string Version,
    string Minecraft,
    ModLoader Loader,
    string LoaderVersion,
    string Description,
    bool HasOverrides,
    int? RecommendedRamMb,
    IReadOnlyList<ModDto> Mods,
    IReadOnlyList<ServerDto> Servers,
    string? CurseForgeUrl = null);

/// <summary>
/// Resultado da importação de um modpack do CurseForge (modelo neutro).
/// </summary>
/// <param name="ServerPackFileId">
/// ID do "server pack" do modpack, quando o autor publica um (senão nulo). O servidor o usa
/// para inferir o <c>ModSide</c> de cada mod (presente no pack ⇒ roda no servidor).
/// </param>
public record ImportedModpackDto(
    string Name,
    string Version,
    string Minecraft,
    ModLoader Loader,
    string LoaderVersion,
    List<ImportedModDto> Mods,
    byte[]? Overrides,
    long? ServerPackFileId = null,
    // Origem CF (para registrar a versão e checar atualizações depois)
    long CurseProjectId = 0,
    long CurseFileId = 0);

/// <summary>
/// Representa um mod importado, contendo informações detalhadas como identificadores,
/// nome, versão, URL de download e o alvo para o qual foi projetado.
/// </summary>
public record ImportedModDto(
    long ModId,
    long FileId,
    string Name,
    string FileName,
    string DownloadUrl,
    string Target,
    string? Version);

/// <summary>Linha de modpack para a tabela do painel admin (resumo + contagens).</summary>
public sealed record ModpackAdminRowDto(
    Guid Id,
    string Name,
    string Version,
    string Minecraft,
    ModLoader Loader,
    string LoaderVersion,
    int ModCount,
    int ServerCount,
    bool IsPublished,
    bool HasOverrides,
    DateTime UpdatedAt,
    string? CurseForgeUrl = null);

/// <summary>Progresso do download de jars durante o Guardar (mod atual / total + nome).</summary>
public sealed record SaveProgressDto(int Current, int Total, string FileName);

/// <summary>
/// Resultado de adicionar um mod da busca: a entrada destacada + se há arquivo <b>compatível</b> com a
/// versão MC + loader do modpack. <c>Compatible == false</c> = o CurseForge não tem arquivo para essa
/// combinação (caiu no mais recente disponível) — avisar o admin para evitar crash do loader.
/// </summary>
public sealed record ModAddResultDto(TCMine_Domain.Entities.ModEntryEntity Entry, bool Compatible);

/// <summary>Badge de um modpack onde um arquivo de mod está presente (id + nome).</summary>
public sealed record ModpackBadgeDto(Guid Id, string Name);

/// <summary>
/// Linha da lista de novidades do painel. <c>ModpackId</c>/<c>ModpackName</c> nulos = notícia
/// **global** (do servidor, não atrelada a modpack).
/// </summary>
public sealed record NewsRowDto(
    int Id,
    Guid? ModpackId,
    string? ModpackName,
    string Tag,
    string Title,
    string Summary,
    DateTime PublishedAt,
    bool IsPublished);

/// <summary>
/// Linha da página "todos os mods" do painel: um <c>ModFile</c> (arquivo único) com os modpacks
/// em que aparece. <c>IsOrphan</c> = sem vínculo com nenhum modpack; <c>IsManual</c> = upload (sem
/// origem CurseForge).
/// </summary>
public sealed record ModFileRowDto(
    long FileId,
    string Name,
    string? Version,
    string FileName,
    long FileLength,
    bool IsManual,
    bool IsOrphan,
    IReadOnlyList<ModpackBadgeDto> Modpacks);

/// <summary>Um arquivo de override com o seu tamanho (caminho relativo + bytes).</summary>
public sealed record OverrideFileDto(string Path, long Length);

/// <summary>
/// Um item (arquivo ou pasta) num **nível** da árvore de overrides — para carregamento preguiçoso
/// (só os filhos diretos de uma pasta). <c>Path</c> é o caminho relativo completo; <c>Name</c> é o
/// segmento exibido; <c>IsFolder</c> distingue pasta de arquivo.
/// </summary>
public sealed record OverrideNodeDto(string Path, string Name, bool IsFolder);

/// <summary>Resultado de um import de modpack do CF para mesclar no rascunho (metadados + mods + overrides).</summary>
public sealed record DraftImportDto<TModEntryEntity>(
    string Name,
    string Version,
    string Minecraft,
    ModLoader Loader,
    string LoaderVersion,
    List<TModEntryEntity> Mods,
    byte[]? Overrides,
    // Origem CF para registrar/atualizar a tabela de import
    long CurseProjectId = 0,
    long CurseFileId = 0,
    // Metadados do projeto CF: resumo (→ descrição) e link da página (→ badge)
    string? Description = null,
    string? CurseForgeUrl = null);

/// <summary>Arquivo mais recente de um mod (do 'latestFilesIndexes' do CF), filtrado por versão+loader.</summary>
public sealed record CfLatestFileDto(long ModId, long FileId, string FileName);

/// <summary>
/// Uma atualização disponível para um mod do modpack: do arquivo atual para o mais recente do CF.
/// Calculada sob demanda (sem cache no banco) pelo botão "Buscar atualizações".
/// </summary>
public sealed record ModUpdateDto(
    long CurseModId,
    string Name,
    long CurrentFileId,
    string? CurrentVersion,
    long LatestFileId,
    string LatestVersion,
    string FileName,
    string DownloadUrl);

/// <summary>Estado de atualização do modpack importado (para o banner/checagem no editor).</summary>
public sealed record ModpackUpdateStatusDto(
    string ProjectName,
    string? InstalledVersion,
    long InstalledFileId,
    long? LatestFileId,
    string? LatestVersion,
    bool UpdateAvailable,
    DateTime? LastCheckedAt);

/// <summary>Uma versão selecionável (Minecraft ou loader) e se é um lançamento estável.</summary>
public sealed record VersionOptionDto(string Version, bool IsRelease)
{
    public bool IsStable => IsRelease;
}