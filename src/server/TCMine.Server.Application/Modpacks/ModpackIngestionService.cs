using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Resolve e baixa uma lista de mods para uma versão, transicionando-a por
///     Resolving → Ready (ou Failed).
///     A resolução acontece uma vez, na publicação. Mil jogadores depois baixam
///     do nosso blob store, sem tocar em Modrinth ou CurseForge.
/// </summary>
public sealed partial class ModpackIngestionService(
    IModpackRepository repository,
    IBlobStore blobStore,
    IEnumerable<IModResolver> resolvers,
    HttpClient http,
    ILogger<ModpackIngestionService> logger)
{
    private readonly ILogger<ModpackIngestionService> _logger = logger;

    public async Task IngestAsync(
        Guid versionId,
        IReadOnlyList<ModIngestionItem> items,
        CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
        {
            LogVersionMissing(versionId);
            return;
        }

        try
        {
            version.MarkResolving();
            await repository.UpdateVersionAsync(version, ct);
        }
        catch (InvalidOperationException ex)
        {
            LogInvalidTransition(versionId, ex.Message);
            return;
        }

        var failures = new List<string>();

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var outcome = await ResolveAndDownloadAsync(version, item, ct);
            if (outcome is not null)
                failures.Add(outcome);
        }

        // Recarrega para gravar o estado final com os arquivos 
        // adicionados. A versão em memória já os tem, mas queremos garantir
        // que a transição de estado e os arquivos persistam juntos.
        if (failures.Count > 0)
            // Falha parcial derruba tudo: um pack incompleto não deve ser
            // publicado. O admin vê a lista e decide (trocar mod, subir manual).
            version.MarkFailed($"Não foi possível resolver: {string.Join("; ", failures)}");
        else
            version.MarkReady();

        await repository.UpdateVersionAsync(version, ct);
    }

    /// <summary>
    ///     Retorna null em sucesso, ou uma descrição do erro em falha.
    /// </summary>
    private async Task<string?> ResolveAndDownloadAsync(
        ModpackVersion version,
        ModIngestionItem item,
        CancellationToken ct)
    {
        // Escolhe o resolver pela origem pedida. Se o CurseForge foi pedido
        // mas está sem API key, IsAvailable é false e caímos no erro.
        var resolver = resolvers.FirstOrDefault(r => r.Origin == item.Origin && r.IsAvailable);
        if (resolver is null)
            return $"{item.ProjectId} (origem {item.Origin} indisponível)";

        var request = new ModRequest(
            item.ProjectId, item.FileId, version.MinecraftVersion, version.Loader);

        var resolution = await resolver.ResolveAsync(request, ct);

        switch (resolution)
        {
            case ModResolution.Resolved resolved:
                return await DownloadAndAttachAsync(version, item, resolved, ct);

            case ModResolution.DistributionDenied denied:
                // O autor proibiu redistribuição. Sem contorno legítimo — o
                // admin precisa subir o arquivo manualmente ou trocar o mod.
                return $"{denied.ProjectName} (autor não permite redistribuição)";

            case ModResolution.NotFound notFound:
                return $"{item.ProjectId} ({notFound.Reason})";

            default:
                return $"{item.ProjectId} (resultado desconhecido)";
        }
    }

    private async Task<string?> DownloadAndAttachAsync(
        ModpackVersion version,
        ModIngestionItem item,
        ModResolution.Resolved resolved,
        CancellationToken ct)
    {
        try
        {
            await using var stream = await http.GetStreamAsync(resolved.DownloadUrl, ct);

            // O blob store recalcula o SHA-256 durante a gravação. Não
            // passamos expectedSha256 porque o Modrinth informa SHA-1/512, e o
            // nosso store é SHA-256 — a integridade fica garantida pelo próprio
            // hash calculado, que vira a identidade do arquivo.
            var sha256 = await blobStore.PutAsync(stream, null,
                "application/java-archive", ct);

            await using var stored = await blobStore.OpenAsync(sha256, ct);

            var path = $"mods/{resolved.FileName}";

            if (version.Files.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                return null; // já presente, não é erro

            version.Files.Add(new ModpackFile
            {
                ModpackVersionId = version.Id,
                Path = path,
                Sha256 = sha256,
                SizeBytes = stored.Length,
                Side = item.Side,
                Origin = item.Origin,
                OriginReference = item.ProjectId
            });

            return null;
        }
        catch (HttpRequestException ex)
        {
            LogDownloadError(ex, resolved.DownloadUrl.ToString());
            return $"{resolved.FileName} (falha no download)";
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Versão {VersionId} não encontrada para ingestão.")]
    private partial void LogVersionMissing(Guid versionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transição inválida na versão {VersionId}: {Reason}")]
    private partial void LogInvalidTransition(Guid versionId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao baixar {Url}.")]
    private partial void LogDownloadError(Exception ex, string url);
}