using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Traz um modpack inteiro de uma origem externa, já traduzido para termos
///     nossos. A Application não sabe o que é um .zip com manifest.json —
///     isso é detalhe do CurseForge e mora na Infrastructure.
/// </summary>
public interface IUpstreamPackSource
{
    ModFileOrigin Origin { get; }

    ValueTask<bool> IsAvailableAsync(CancellationToken ct);

    /// <summary>Procura packs (não mods) pelo nome.</summary>
    Task<IReadOnlyList<UpstreamPackSummary>> SearchPacksAsync(string text, int limit, CancellationToken ct);

    /// <summary>
    ///     Baixa e lê um pack. <paramref name="fileId" /> nulo = release mais
    ///     recente. Devolve null quando o pack ou a release não existem.
    /// </summary>
    Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct);

    /// <summary>Id da release mais recente, para detectar atualização sem baixar o pack.</summary>
    Task<UpstreamRelease?> GetLatestReleaseAsync(string projectId, CancellationToken ct);
}

/// <summary>Resultado de busca de packs.</summary>
public sealed record UpstreamPackSummary(
    string ProjectId,
    string Name,
    string Summary,
    string? IconUrl,
    string? Author);

/// <summary>Uma release do pack na origem.</summary>
public sealed record UpstreamRelease(string FileId, string Label, DateTimeOffset PublishedAt);

/// <summary>
///     Pack lido da origem: o que precisa virar Modpack + ModpackVersion aqui.
/// </summary>
public sealed record UpstreamPack
{
    public required string ProjectId { get; init; }
    public required string FileId { get; init; }

    /// <summary>Rótulo da versão na origem (ex.: "4.2.1"), separado da nossa numeração.</summary>
    public required string VersionLabel { get; init; }

    public required string Name { get; init; }
    public string? Author { get; init; }

    /// <summary>Capa do pack na origem. Best-effort: nulo não impede a importação.</summary>
    public string? IconUrl { get; init; }
    public required string MinecraftVersion { get; init; }
    public required ModLoader Loader { get; init; }

    /// <summary>Versão do loader declarada no pack, quando informada.</summary>
    public string? LoaderVersion { get; init; }

    /// <summary>Mods a resolver e baixar (id do projeto + release fixada).</summary>
    public required IReadOnlyList<UpstreamPackMod> Mods { get; init; }

    /// <summary>Arquivos da pasta overrides (configs, scripts), já em memória.</summary>
    public required IReadOnlyList<UpstreamPackOverride> Overrides { get; init; }

    /// <summary>
    ///     Página do "server pack" que o autor publicou junto, quando existe.
    ///     Guardamos o link e não o conteúdo: o TCMine monta a instância do
    ///     servidor a partir da versão importada, então o zip do autor não entra
    ///     no fluxo — ele serve de referência para quem quer comparar configs ou
    ///     rodar o servidor oficial ao lado.
    /// </summary>
    public string? ServerPackUrl { get; init; }
}

/// <summary>
///     Um mod do pack. <paramref name="Name" /> é nulo quando a origem não o
///     informou — o manifest do CurseForge só traz ids, o nome vem de uma
///     consulta em lote à parte.
/// </summary>
public sealed record UpstreamPackMod(string ProjectId, string FileId, bool Required, string? Name = null);

/// <summary>Um arquivo de override. Conteúdo em bytes: packs têm centenas de configs pequenos.</summary>
public sealed record UpstreamPackOverride(string Path, byte[] Content);
