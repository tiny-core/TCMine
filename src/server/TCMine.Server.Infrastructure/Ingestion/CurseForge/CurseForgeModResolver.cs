using TCMine.Contracts.Modpacks;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

/// <summary>
///     Resolve mods do CurseForge.
///     Segunda opção depois do Modrinth: exige API key e nem todo autor permite
///     redistribuição — quando não permite, a API devolve downloadUrl nulo e nós
///     traduzimos isso em <see cref="ModResolution.DistributionDenied" />, para a
///     UI poder pedir o envio manual do arquivo em vez de falhar sem explicação.
/// </summary>
public sealed partial class CurseForgeModResolver(
    CurseForgeApiClient api,
    ILogger<CurseForgeModResolver> logger) : IModResolver
{
    private readonly ILogger<CurseForgeModResolver> _logger = logger;

    public ModFileOrigin Origin => ModFileOrigin.CurseForge;

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct) =>
        new(api.HasApiKeyAsync(ct));

    public async Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
    {
        // No CurseForge a identidade do projeto é o id numérico.
        if (!int.TryParse(request.ProjectId, out var modId))
            return new ModResolution.NotFound($"'{request.ProjectId}' não é um id de projeto do CurseForge.");

        try
        {
            var files = await FindFilesAsync(modId, request, ct);
            if (files is null || files.Count is 0)
            {
                return new ModResolution.NotFound(
                    $"Nenhum arquivo do projeto {modId} para Minecraft {request.MinecraftVersion} com {request.Loader}.");
            }

            // FileId específico quando pedido; senão o mais recente compatível.
            var file = request.FileId is not null && int.TryParse(request.FileId, out var wantedId)
                ? files.FirstOrDefault(f => f.Id == wantedId) ?? files[0]
                : files[0];

            // Confere o arquivo escolhido em vez de confiar no filtro da query.
            // O gameVersions do CurseForge mistura versão do MC com loader e
            // com as tags de ambiente, então basta procurar a versão pedida ali.
            // Um mod da versão errada não falha aqui: instala e derruba o
            // servidor no arranque, com um erro que não aponta para cá.
            if (!file.GameVersions.Contains(request.MinecraftVersion, StringComparer.OrdinalIgnoreCase))
            {
                return new ModResolution.NotFound(
                    $"O arquivo escolhido do projeto {modId} declara "
                    + $"[{string.Join(", ", file.GameVersions)}], e não Minecraft {request.MinecraftVersion}.");
            }

            // Uma consulta só ao projeto: serve tanto para o nome (na recusa de
            // redistribuição) quanto para o ícone (no caminho feliz).
            var mod = await GetModAsync(modId, ct);

            // Sem downloadUrl = autor negou redistribuição por terceiros. Levamos
            // o admin à página do projeto para ele baixar e enviar à mão.
            if (string.IsNullOrWhiteSpace(file.DownloadUrl))
            {
                var page = mod?.Slug is { Length: > 0 } slug
                    ? new Uri($"https://www.curseforge.com/minecraft/mc-mods/{slug}")
                    : new Uri($"https://www.curseforge.com/projects/{modId.ToString(CultureInfo.InvariantCulture)}");

                return new ModResolution.DistributionDenied(mod?.Name ?? $"Projeto {modId}", page);
            }

            var dependencies = file.Dependencies
                .Where(d => d.ModId > 0)
                .Select(d => new ModDependency(
                    d.ModId.ToString(CultureInfo.InvariantCulture),
                    MapDependencyKind(d.RelationType)))
                .ToList();

            // O CurseForge não expõe SHA-256 — só SHA-1 (algo 1) e MD5 (algo 2).
            // O SHA-256 real sai do download, calculado pelo blob store.
            var sha1 = file.Hashes.FirstOrDefault(h => h.Algo == 1)?.Value;

            var iconUrl = mod?.Logo?.ThumbnailUrl;

            return new ModResolution.Resolved(
                file.Id.ToString(CultureInfo.InvariantCulture),
                file.FileName ?? $"{modId}-{file.Id}.jar",
                sha1,
                file.FileLength,
                new Uri(file.DownloadUrl),
                dependencies,
                iconUrl,
                SideOf(file.GameVersions));
        }
        catch (HttpRequestException ex)
        {
            LogResolveError(ex, request.ProjectId);
            return new ModResolution.NotFound("Falha ao consultar o CurseForge.");
        }
    }

    private async Task<IReadOnlyList<CurseForgeFile>?> FindFilesAsync(
        int modId, ModRequest request, CancellationToken ct)
    {
        var url = $"/v1/mods/{modId}/files"
                  + $"?gameVersion={Uri.EscapeDataString(request.MinecraftVersion)}";

        var loaderType = CurseForgeApiClient.ToLoaderType(request.Loader);
        if (loaderType is not 0)
            url += $"&modLoaderType={loaderType}";

        var response = await api.GetAsync(
            url, CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeFile, ct);

        // A API já devolve do mais novo para o mais antigo, mas não é contrato:
        // ordenamos para a escolha do "mais recente" ser determinística.
        return response?.Data?.OrderByDescending(f => f.FileDate).ToList();
    }

    private async Task<CurseForgeMod?> GetModAsync(int modId, CancellationToken ct)
    {
        var response = await api.GetAsync(
            $"/v1/mods/{modId}", CurseForgeJsonContext.Default.CurseForgeResponseCurseForgeMod, ct);
        return response?.Data;
    }

    private static ModDependencyKind MapDependencyKind(int relationType) => relationType switch
    {
        1 => ModDependencyKind.Embedded,
        2 => ModDependencyKind.Optional,
        3 => ModDependencyKind.Required,
        5 => ModDependencyKind.Incompatible,
        _ => ModDependencyKind.Optional // tool/include: não puxa nada
    };

    /// <summary>
    ///     Lado do mod conforme as tags de ambiente do CurseForge.
    ///     Elas não são um campo próprio: o CurseForge modela ambiente como
    ///     "game version", então "Client" e "Server" chegam misturados com
    ///     "26.1.2" e "NeoForge" na mesma lista. Obrigatórias para mods de
    ///     Minecraft desde 15/07/2026 — arquivos publicados antes podem não ter
    ///     nenhuma, e aí devolvemos null (desconhecido) em vez de chutar.
    /// </summary>
    private static FileSide? SideOf(IReadOnlyList<string> gameVersions)
    {
        var client = gameVersions.Contains("Client", StringComparer.OrdinalIgnoreCase);
        var server = gameVersions.Contains("Server", StringComparer.OrdinalIgnoreCase);

        return (client, server) switch
        {
            (true, false) => FileSide.ClientOnly,
            (false, true) => FileSide.ServerOnly,
            (true, true) => FileSide.Both,
            _ => null // sem tag: o arquivo é anterior à exigência
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro ao resolver '{ProjectId}' no CurseForge.")]
    private partial void LogResolveError(Exception ex, string projectId);
}
