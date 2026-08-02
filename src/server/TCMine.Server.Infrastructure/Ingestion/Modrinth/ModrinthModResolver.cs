using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

/// <summary>
///     Resolve mods do Modrinth.
///     Preferido sobre o CurseForge: não exige API key e a licença de publicação
///     do Modrinth já garante redistribuição, então o caso DistributionDenied
///     nunca acontece aqui.
/// </summary>
public sealed partial class ModrinthModResolver(
    HttpClient http,
    ILogger<ModrinthModResolver> logger) : IModResolver
{
    private readonly ILogger<ModrinthModResolver> _logger = logger;

    public ModFileOrigin Origin => ModFileOrigin.Modrinth;

    // Sempre disponível: não depende de configuração nenhuma.
    public bool IsAvailable => true;

    public async Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
    {
        var loader = ToModrinthLoader(request.Loader);

        // A API aceita filtros de game_versions e loaders como arrays JSON na
        // query string. Pedimos só as versões compatíveis, já ordenadas por
        // data de publicação (mais recente primeiro é o padrão da API).
        var url =
            $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(request.ProjectId)}/version" +
            $"?game_versions=[\"{request.MinecraftVersion}\"]" +
            $"&loaders=[\"{loader}\"]";

        try
        {
            var versions = await http.GetFromJsonAsync(
                url, ModrinthJsonContext.Default.IReadOnlyListModrinthVersion, ct);

            if (versions is null || versions.Count is 0)
            {
                return new ModResolution.NotFound(
                    $"Nenhuma versão de '{request.ProjectId}' para Minecraft {request.MinecraftVersion} com {loader}.");
            }

            // Se um FileId específico foi pedido, procura por ele; senão pega a
            // versão mais recente compatível.
            var version = request.FileId is not null
                ? versions.FirstOrDefault(v => v.Id == request.FileId) ?? versions[0]
                : versions[0];

            // Uma versão pode ter vários arquivos (o jar e um sources, por
            // exemplo). O "primary" é o que interessa; se nenhum for marcado,
            // o primeiro serve.
            var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files[0];

            var dependencies = version.Dependencies
                .Where(d => d.ProjectId is { Length: > 0 })
                .Select(d => new ModDependency(d.ProjectId!, MapDependencyKind(d.DependencyType)))
                .ToList();

            return new ModResolution.Resolved(
                version.Id,
                file.Filename,
                file.Hashes?.Sha1,
                file.Size,
                new Uri(file.Url),
                dependencies);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return new ModResolution.NotFound($"Projeto '{request.ProjectId}' não encontrado no Modrinth.");
        }
        catch (HttpRequestException ex)
        {
            LogResolveError(ex, request.ProjectId);
            return new ModResolution.NotFound("Falha ao consultar o Modrinth.");
        }
    }

    // O Modrinth usa strings; convertemos para o enum do domínio de resolução.
    private static ModDependencyKind MapDependencyKind(string? type)
    {
        return type switch
        {
            "required" => ModDependencyKind.Required,
            "optional" => ModDependencyKind.Optional,
            "incompatible" => ModDependencyKind.Incompatible,
            "embedded" => ModDependencyKind.Embedded,
            _ => ModDependencyKind.Optional // desconhecido: trata como opcional (não puxa)
        };
    }

    // O nome do loader no domínio difere do que a API do Modrinth espera.
    private static string ToModrinthLoader(ModLoader loader)
    {
        return loader switch
        {
            ModLoader.Forge => "forge",
            ModLoader.NeoForge => "neoforge",
            ModLoader.Fabric => "fabric",
            ModLoader.Quilt => "quilt",
            ModLoader.Vanilla => "minecraft",
            _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null)
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro ao resolver '{ProjectId}' no Modrinth.")]
    private partial void LogResolveError(Exception ex, string projectId);
}
