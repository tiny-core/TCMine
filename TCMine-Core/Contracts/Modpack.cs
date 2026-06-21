using TCMine_Core.modpack;

namespace TCMine_Core.Contracts;

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
    IReadOnlyList<ServerDto> Servers);

/// <summary>
/// Resultado da importação de um modpack do CurseForge (modelo neutro).
/// </summary>
/// <param name="ServerPackFileId">
/// Id do "server pack" do modpack, quando o autor publica um (senão nulo). O servidor o usa
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
    long? ServerPackFileId = null);

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
    DateTime UpdatedAt);

/// <summary>Progresso do download de jars durante o Guardar (mod atual / total + nome).</summary>
public sealed record SaveProgressDto(int Current, int Total, string FileName);

/// <summary>Um arquivo de override com o seu tamanho (caminho relativo + bytes).</summary>
public sealed record OverrideFileDto(string Path, long Length);

/// <summary>Resultado de um import de modpack do CF para mesclar no rascunho (metadados + mods + overrides).</summary>
public sealed record DraftImportDto<TModEntryEntity>(
    string Name,
    string Version,
    string Minecraft,
    ModLoader Loader,
    string LoaderVersion,
    List<TModEntryEntity> Mods,
    byte[]? Overrides);
    
    
/// <summary>Uma versão selecionável (Minecraft ou loader) e se é um lançamento estável.</summary>
public sealed record VersionOptionDto(string Version, bool IsRelease)
{
    public bool IsStable => IsRelease;
}