using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Resolve e baixa uma lista de mods para uma versão, transicionando-a por
///     Resolving → Draft (ou Failed). Publicar é um ato separado do admin.
///     A resolução acontece uma vez, na ingestão. Mil jogadores depois baixam do
///     nosso blob store, sem tocar em Modrinth ou CurseForge.
/// </summary>
public sealed partial class ModpackIngestionService(
    IModpackRepository repository,
    IBlobStore blobStore,
    IEnumerable<IModResolver> resolvers,
    IModDownloader downloader,
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

        // Fila de trabalho: começa com os itens pedidos e cresce com as
        // dependências requeridas de cada mod resolvido (transitivo).
        // 'processed' evita retrabalho e corta ciclos; 'existing' pula
        // dependências já satisfeitas na versão.
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existing = version.Files
            .Where(f => f.ProjectSlug is not null)
            .Select(f => f.ProjectSlug!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var work = new Queue<(ModIngestionItem Item, bool IsDependency)>();
        foreach (var it in items)
            work.Enqueue((it, false));

        var failures = new List<string>();

        while (work.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (item, isDependency) = work.Dequeue();

            // Dependência já satisfeita, ou já tratada nesta corrida: pula.
            // Itens explícitos do admin sempre passam (permite atualizar um já
            // presente, substituindo via UpsertFile).
            if (isDependency && (existing.Contains(item.ProjectId) || processed.Contains(item.ProjectId)))
                continue;

            if (!processed.Add(item.ProjectId))
                continue;

            var outcome = await ResolveAndDownloadAsync(version, item, ct);
            if (outcome.Error is not null)
            {
                failures.Add(outcome.Error);
                continue;
            }

            // Enfileira as requeridas que ainda não temos. A dependência herda
            // o lado do mod que a pediu (lib de mod cliente = cliente, etc.).
            foreach (var depId in outcome.RequiredDependencies)
                if (!processed.Contains(depId) && !existing.Contains(depId))
                    work.Enqueue((new ModIngestionItem(item.Origin, depId, null, item.Side), true));
        }

        // Grava o estado final com os arquivos adicionados.
        if (failures.Count > 0)
            // Falha parcial derruba tudo: um pack incompleto não deve ser
            // publicado. O admin vê a lista e decide (trocar mod, subir manual).
            version.MarkFailed($"Não foi possível resolver: {string.Join("; ", failures)}");
        else
            // Resolveu e baixou tudo — mas quem publica é o admin, após
            // revisar. Publicar automático tornaria a versão imutável antes de
            // ele poder trocar um mod.
            version.ReturnToDraft();

        await repository.UpdateVersionAsync(version, ct);
    }

    /// <summary>Retorna null em sucesso…</summary>
    private async Task<ResolveOutcome> ResolveAndDownloadAsync(
        ModpackVersion version,
        ModIngestionItem item,
        CancellationToken ct)
    {
        // Escolhe o resolver pela origem pedida. Se o CurseForge foi pedido,
        // mas está sem API key, IsAvailable é false e caímos no erro.
        var resolver = resolvers.FirstOrDefault(r => r.Origin == item.Origin && r.IsAvailable);
        if (resolver is null)
            return ResolveOutcome.Fail($"{item.ProjectId} (origem {item.Origin} indisponível)");

        var request = new ModRequest(
            item.ProjectId, item.FileId, version.MinecraftVersion, version.Loader);

        var resolution = await resolver.ResolveAsync(request, ct);

        switch (resolution)
        {
            case ModResolution.Resolved resolved:
            {
                var error = await DownloadAndAttachAsync(version, item, resolved, ct);
                if (error is not null)
                    return ResolveOutcome.Fail(error);

                // Só as REQUERIDAS entram na fila. Embedded já vem dentro do jar;
                // optional é escolha do usuário; incompatible nunca se puxa.
                var required = resolved.Dependencies
                    .Where(d => d.Kind is ModDependencyKind.Required)
                    .Select(d => d.ProjectId)
                    .ToList();

                return ResolveOutcome.Ok(required);
            }

            case ModResolution.DistributionDenied denied:
                return ResolveOutcome.Fail($"{denied.ProjectName} (autor não permite redistribuição)");

            case ModResolution.NotFound notFound:
                return ResolveOutcome.Fail($"{item.ProjectId} ({notFound.Reason})");

            default:
                return ResolveOutcome.Fail($"{item.ProjectId} (resultado desconhecido)");
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
            await using var stream = await downloader.OpenAsync(resolved.DownloadUrl, ct);

            // O blob store recalcula o SHA-256 durante a gravação. Não
            // passamos expectedSha256 porque o Modrinth informa SHA-1/512, e o
            // nosso store é SHA-256 — a integridade fica garantida pelo próprio
            // hash calculado, que vira a identidade do arquivo.
            var sha256 = await blobStore.PutAsync(stream, null,
                "application/java-archive", ct);

            await using var stored = await blobStore.OpenAsync(sha256, ct);

            var path = $"mods/{resolved.FileName}";

            // Mesmo mod, mesmo conteúdo já presente? Nada a fazer — evita
            // remover e re-adicionar a mesma linha numa re-ingestão.
            if (version.Files.Any(f =>
                    string.Equals(f.ProjectSlug, item.ProjectId, StringComparison.OrdinalIgnoreCase)
                    && f.Sha256 == sha256))
                return null;

            // Conflito raro: outro mod já ocupa este caminho. Dois arquivos no
            // mesmo path não podem coexistir na instância.
            if (version.Files.Any(f =>
                    f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(f.ProjectSlug, item.ProjectId, StringComparison.OrdinalIgnoreCase)))
                return $"{resolved.FileName} (conflito de caminho com outro mod)";

            var file = new ModpackFile
            {
                ModpackVersionId = version.Id,
                ProjectSlug = item.ProjectId, // identidade estável do mod
                Path = path,
                Sha256 = sha256,
                SizeBytes = stored.Length,
                Side = item.Side,
                Origin = item.Origin,
                OriginReference = resolved.VersionId // id da versão fixada (base do check de updates)
            };

            // Mesmo mod em outra versão do .jar (jei-1.2.0 → jei-1.5)? Substitui,
            // nunca acumula dois arquivos do mesmo mod em mods/. O UpsertFile
            // devolve o ID do arquivo trocado para apagarmos a linha antiga —
            // o UpdateVersionAsync final (Update num grafo destacado) não apaga
            // filhos removidos da coleção sozinho.
            var replacedId = version.UpsertFile(file);
            if (replacedId is { } oldId)
                await repository.RemoveFileAsync(version.Id, oldId, ct);

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

    // Resultado de resolver+baixar um item: erro (se houve) e as dependências
    // requeridas a puxar em seguida.
    private sealed record ResolveOutcome(string? Error, IReadOnlyList<string> RequiredDependencies)
    {
        public static ResolveOutcome Ok(IReadOnlyList<string> deps)
        {
            return new ResolveOutcome(null, deps);
        }

        public static ResolveOutcome Fail(string error)
        {
            return new ResolveOutcome(error, []);
        }
    }
}