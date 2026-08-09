using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
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
    IJobProgressReporter progress,
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

        // MC e loader vêm do modpack agora. Carrega uma vez; o loop reusa.
        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack is null)
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

        // Nomes legíveis, quando a importação os gravou. "Baixando Just Enough
        // Items" diz algo; "Baixando 238222" não.
        var names = UpstreamSnapshot.FromJson(version.UpstreamSnapshotJson)?.Names
                    ?? new Dictionary<string, string>();

        // O total é o do pack e NÃO se mexe: dependências transitivas são
        // contadas à parte, senão o denominador cresceria enquanto baixa e a
        // barra andaria para trás.
        var total = work.Count;
        var done = 0;
        var deps = 0;
        var title = $"Resolvendo {modpack.Name} {version.Version}";

        void Report(string step) =>
            progress.Report(versionId, new JobProgress(title, step, done, total) { Dependencies = deps });

        while (work.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (item, isDependency) = work.Dequeue();

            var label = names.GetValueOrDefault(item.ProjectId, item.ProjectId);

            // Dependência já satisfeita, ou já tratada nesta corrida: pula.
            // Itens explícitos do admin sempre passam (permite atualizar um já
            // presente, substituindo via UpsertFile).
            if (isDependency && (existing.Contains(item.ProjectId) || processed.Contains(item.ProjectId)))
            {
                // Já satisfeita: avança o contador mesmo assim. Sem isto, uma
                // reingestão passava por centenas de itens sem a barra mexer e
                // parecia travada até tudo aparecer de uma vez no fim.
                deps++;
                Report($"{label} — já presente");
                continue;
            }

            if (!processed.Add(item.ProjectId))
                continue;

            // Item explícito já presente NÃO é pulado: é assim que se atualiza um
            // mod (o UpsertFile troca o .jar). Só o feedback muda.
            Report(existing.Contains(item.ProjectId) ? $"Verificando {label}" : $"Baixando {label}");

            var outcome = await ResolveAndDownloadAsync(version, item, modpack.MinecraftVersion, modpack.Loader, ct);

            if (isDependency)
                deps++;
            else
                done++;

            if (outcome.Pending is { } pending)
            {
                // Não é falha da versão: fica registrado para upload manual.
                version.UpsertPending(pending);
                continue;
            }

            if (outcome.Error is not null)
            {
                failures.Add(outcome.Error);
                continue;
            }

            // Chegou o mod que faltava — a pendência (se havia) morre aqui.
            if (version.ResolvePending(item.ProjectId) is { } resolvedPendingId)
                await repository.RemovePendingAsync(version.Id, resolvedPendingId, ct);

            // Enfileira as requeridas que ainda não temos. A dependência herda
            // o lado do mod que a pediu (lib de mod cliente = cliente, etc.).
            foreach (var depId in outcome.RequiredDependencies)
            {
                if (!processed.Contains(depId) && !existing.Contains(depId))
                    work.Enqueue((new ModIngestionItem(item.Origin, depId, null, item.Side), true));
            }
        }

        // Um último retrato com os contadores fechados: sem ele o acompanhamento
        // congelava no penúltimo item, porque o Report acontece ANTES de
        // processar cada um.
        Report("Finalizando");

        // Grava o estado final com os arquivos adicionados.
        if (failures.Count > 0)
        {
            // Sobrou falha de verdade (conflito de caminho, origem sem API key):
            // aí a versão não tem como seguir e o admin precisa intervir.
            version.MarkFailed($"Não foi possível resolver: {string.Join("; ", failures)}");
            progress.Complete(versionId, version.FailureReason);
        }
        else
        {
            // Resolveu o que dava — mods sem redistribuição viraram pendência, não
            // reprovação. Quem publica é o admin, após revisar as pendências.
            version.ReturnToDraft();
            progress.Complete(versionId);
        }

        await repository.UpdateVersionAsync(version, ct);
    }

    /// <summary>Retorna null em sucesso…</summary>
    private async Task<ResolveOutcome> ResolveAndDownloadAsync(
        ModpackVersion version,
        ModIngestionItem item,
        string minecraftVersion,
        ModLoader loader,
        CancellationToken ct)
    {
        // Escolhe o resolver pela origem pedida. Se o CurseForge foi pedido, mas
        // está sem API key, ele se declara indisponível e caímos no erro.
        IModResolver? resolver = null;
        foreach (var candidate in resolvers.Where(r => r.Origin == item.Origin))
        {
            if (!await candidate.IsAvailableAsync(ct))
                continue;

            resolver = candidate;
            break;
        }

        if (resolver is null)
            // Origem inteira fora (sem API key, por exemplo): isso não é pendência
            // de um mod, é configuração faltando — a versão precisa reprovar.
            return ResolveOutcome.Fail($"{item.ProjectId} (origem {item.Origin} indisponível)");

        var request = new ModRequest(item.ProjectId, item.FileId, minecraftVersion, loader);

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
                // Decisão do autor, não erro nosso: nenhuma nova tentativa muda
                // isso. Vira pendência para o admin subir o .jar à mão.
                return ResolveOutcome.Postpone(new PendingMod
                {
                    ModpackVersionId = version.Id,
                    ProjectSlug = item.ProjectId,
                    DisplayName = denied.ProjectName,
                    Origin = item.Origin,
                    FileId = item.FileId,
                    Side = item.Side,
                    Reason = PendingModReason.DistributionDenied,
                    Detail = "O autor não permite redistribuição automática.",
                    PageUrl = denied.ProjectPage.ToString()
                });

            case ModResolution.NotFound notFound:
                return ResolveOutcome.Postpone(new PendingMod
                {
                    ModpackVersionId = version.Id,
                    ProjectSlug = item.ProjectId,
                    DisplayName = item.ProjectId,
                    Origin = item.Origin,
                    FileId = item.FileId,
                    Side = item.Side,
                    Reason = PendingModReason.NoCompatibleFile,
                    Detail = notFound.Reason
                });

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
                // O lado declarado pela origem ganha do pedido: o Modrinth sabe
                // se o mod roda no cliente ou no servidor, nós só supomos.
                Side = resolved.Side ?? item.Side,
                Optional = item.Optional,
                Origin = item.Origin,
                OriginReference = resolved.VersionId, // id da versão fixada (base do check de updates)
                IconUrl = resolved.IconUrl // cosmético: exibido na grade de mods
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
    private sealed record ResolveOutcome(
        string? Error,
        IReadOnlyList<string> RequiredDependencies,
        PendingMod? Pending = null)
    {
        public static ResolveOutcome Ok(IReadOnlyList<string> deps) => new(null, deps);

        public static ResolveOutcome Fail(string error) => new(error, []);

        public static ResolveOutcome Postpone(PendingMod pending) => new(null, [], pending);
    }
}
