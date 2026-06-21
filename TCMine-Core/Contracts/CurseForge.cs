using System.Text.Json.Serialization;

namespace TCMine_Core.Contracts;

/// <summary>
/// Representa um carregador em um manifesto, identificando seu ID e se é um carregador primário.
/// </summary>
public record CfManifestLoaderDto(string Id, bool Primary);

/// <summary>
/// Representa informações específicas do Minecraft associadas a um manifesto,
/// incluindo a versão e os carregadores de mods aplicáveis.
/// </summary>
public record CfManifestMcDto(string Version, List<CfManifestLoaderDto> ModLoaders);

/// <summary>
/// Representa um arquivo no manifesto CurseForge, contendo informações sobre o projeto,
/// o arquivo e se é um componente obrigatório.
/// </summary>
public record CfManifestFileDto(
    [property: JsonPropertyName("projectID")]
    long ProjectId,
    [property: JsonPropertyName("fileID")] long FileId,
    bool Required);

/// <summary>
/// Representa um manifesto CurseForge, contendo informações sobre o Minecraft,
/// o nome e versão do manifesto, arquivos associados, e possíveis sobrescritas.
/// </summary>
public record CfManifestDto(
    CfManifestMcDto Minecraft,
    string? Name,
    string? Version,
    List<CfManifestFileDto> Files,
    string? Overrides = "overrides");

/// <summary>
/// Representa a referência a um arquivo associado a um mod no CurseForge.
/// Contém informações como o ID do arquivo, o ID do mod, o nome do arquivo,
/// a URL de download e um nome de exibição opcional.
///
/// <see cref="ServerPackFileId"/> aponta para o "server pack" que alguns autores publicam
/// junto do modpack — usado depois (import) para inferir o <c>ModSide</c> de cada mod.
/// É nulo quando o autor não fornece um pack de servidor.
/// </summary>
public record CfFileRefDto(
    long Id,
    long ModId,
    string FileName,
    string? DownloadUrl,
    string? DisplayName,
    long? ServerPackFileId = null);

/// <summary>
/// Representa uma referência a um mod no CurseForge, contendo informações como
/// o identificador do mod, o nome e sua classe associada.
/// </summary>
public record CfModRefDto(long Id, string Name, long ClassId);

/// <summary>
/// Resultado de uma pesquisa no CurseForge (mod ou modpack) para a UI de busca — nome,
/// resumo e logo, além da classe (que determina o destino: mod/resourcepack/shaderpack).
/// </summary>
public record CfSearchResultDto(long Id, string Name, string? Summary, string? LogoUrl, long ClassId);