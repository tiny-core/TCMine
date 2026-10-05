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
    IModJarInspector jarInspector,
    IJobProgressReporter progress,
    ILogger<ModpackIngestionService> logger)
{
    /// <summary>
    ///     De quantos em quantos arquivos a ingestão descarrega no banco.
    ///     Existe porque gravar só no fim tinha dois defeitos: o painel mostrava
    ///     "0 mods" durante os vinte minutos de um pack grande, e uma queda no
    ///     meio perdia TODAS as linhas (os bytes sobreviviam no blob store, mas a
    ///     versão voltava vazia). Com o lote, perde-se no máximo o último punhado.
    /// </summary>
    private const int FlushEvery = 25;

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

        // Arquivos ainda não descarregados no banco.
        var unsaved = new List<ModpackFile>();

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

        // Contadores num objeto: o laço vive noutro método (para o tratamento de
        // erro ficar legível) e precisa mexer nos mesmos números que o Report lê.
        var contagem = new Counters();
        var title = $"Resolvendo {modpack.Name} {version.Version}";

        void Report(string step) =>
            progress.Report(versionId, new JobProgress(title, step, contagem.Done, total)
                { Dependencies = contagem.Deps });

        try
        {
            await ProcessAsync(
                version, modpack, work, processed, existing, unsaved, failures, contagem, names, Report, ct);
        }
        catch (OperationCanceledException)
        {
            // Desligamento: a reconciliação do arranque devolve a versão ao
            // estado honesto. Aqui só não se pode fingir que terminou.
            throw;
        }
        catch (Exception ex)
        {
            // Qualquer erro inesperado no meio do laço deixava a versão presa em
            // "Resolvendo" e o acompanhamento girando para sempre — e as
            // dependências que ainda não tinham sido descobertas nunca chegavam.
            LogUnexpected(ex, versionId);
            failures.Add($"erro inesperado: {ex.Message}");
        }

        // Sobra do último lote.
        if (unsaved.Count > 0)
        {
            await repository.AddFilesAsync(version.Id, unsaved, ct);
            unsaved.Clear();
        }

        Report("Finalizando");

        if (failures.Count > 0)
            version.MarkFailed($"Não foi possível resolver: {string.Join("; ", failures)}");
        else
            version.ReturnToDraft();

        await repository.SaveVersionStateAsync(version, ct);

        progress.Complete(versionId, failures.Count > 0 ? version.FailureReason : null);
    }

    /// <summary>O laço em si, separado para o tratamento de erro ficar legível.</summary>
    private async Task ProcessAsync(
        ModpackVersion version,
        Modpack modpack,
        Queue<(ModIngestionItem Item, bool IsDependency)> work,
        HashSet<string> processed,
        HashSet<string> existing,
        List<ModpackFile> unsaved,
        List<string> failures,
        Counters contagem,
        IReadOnlyDictionary<string, string> names,
        Action<string> report,
        CancellationToken ct)
    {
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
                contagem.Deps++;
                report($"{label} — já presente");
                continue;
            }

            if (!processed.Add(item.ProjectId))
                continue;

            // Item explícito já presente NÃO é pulado: é assim que se atualiza um
            // mod (o UpsertFile troca o .jar). Só o feedback muda.
            report(existing.Contains(item.ProjectId) ? $"Verificando {label}" : $"Baixando {label}");

            var outcome = await ResolveAndDownloadAsync(
                version, item, modpack, unsaved, label, ct);

            if (unsaved.Count >= FlushEvery)
            {
                await repository.AddFilesAsync(version.Id, unsaved, ct);
                unsaved.Clear();
            }

            if (isDependency)
                contagem.Deps++;
            else
                contagem.Done++;

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
    }

    /// <summary>Retorna null em sucesso…</summary>
    private async Task<ResolveOutcome> ResolveAndDownloadAsync(
        ModpackVersion version,
        ModIngestionItem item,
        Modpack modpack,
        List<ModpackFile> unsaved,
        string label,
        CancellationToken ct)
    {
        var minecraftVersion = modpack.MinecraftVersion;
        var loader = modpack.Loader;

        // Ordem da ingestão: BANCO → DISCO → REDE. Com a release fixada (pack
        // importado, versão escolhida pelo admin) dá para saber, sem perguntar
        // nada à origem, se este arquivo já passou por aqui — em qualquer
        // modpack. Se o banco o conhece e os bytes estão no blob store, a origem
        // nem é consultada: reimportar um pack, ou importar um segundo pack que
        // partilha metade dos mods, deixa de custar gigabytes e minutos de API.
        if (item.FileId is { Length: > 0 } fileId)
        {
            var reused = await TryReuseIngestedAsync(version, item, fileId, unsaved, ct);
            if (reused is not null)
                return reused;
        }

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
                var outcome = await DownloadAndAttachAsync(version, item, resolved, unsaved, ct);
                return outcome ?? ResolveOutcome.Ok(RequiredOf(resolved));
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
                    PageUrl = denied.ProjectPage.ToString(),
                    Folder = denied.Folder
                });

            case ModResolution.NotFound notFound:
                // Nome e link, como na recusa de redistribuição. Antes esta
                // pendência nascia com o ID cru do projeto e sem link — a tela
                // listava "306612" e o admin não tinha como saber que mod era,
                // nem para onde ir buscá-lo.
                //
                // O nome vem do snapshot da origem (o próprio pack traz o nome
                // de cada mod), então não custa consulta nenhuma; o link se
                // deriva da origem mais o id.
                return ResolveOutcome.Postpone(new PendingMod
                {
                    ModpackVersionId = version.Id,
                    ProjectSlug = item.ProjectId,
                    DisplayName = label,
                    Origin = item.Origin,
                    FileId = item.FileId,
                    Side = item.Side,
                    Reason = PendingModReason.NoCompatibleFile,
                    Detail = notFound.Reason,
                    PageUrl = UpstreamLinks.ProjectPage(item.Origin, item.ProjectId),
                    Folder = notFound.Folder
                });

            default:
                return ResolveOutcome.Fail($"{item.ProjectId} (resultado desconhecido)");
        }
    }

    /// <summary>
    ///     Os dois primeiros degraus (banco e disco) para um item de release
    ///     conhecida. Devolve null quando não dá para reusar e a origem tem de
    ///     ser consultada.
    /// </summary>
    private async Task<ResolveOutcome?> TryReuseIngestedAsync(
        ModpackVersion version,
        ModIngestionItem item,
        string fileId,
        List<ModpackFile> unsaved,
        CancellationToken ct)
    {
        var known = await repository.FindIngestedFileAsync(item.Origin, fileId, ct);

        // Sem as dependências gravadas (arquivo de antes do campo existir), reusar
        // pularia as bibliotecas do mod. Uma ida à origem resolve isso de vez: o
        // download continua evitado pelo degrau do disco em DownloadAndAttachAsync,
        // e a linha nova já sai com as dependências para a próxima.
        if (known?.RequiredDependencyIds() is not { } deps)
            return null;

        // O banco conhece o arquivo, mas o blob pode ter sumido (disco trocado,
        // limpeza manual). Aí o degrau certo é a rede, não um ponteiro morto.
        if (!await blobStore.ExistsAsync(known.Sha256, ct))
            return null;

        var folder = Path.GetDirectoryName(known.Path)?.Replace('\\', '/') ?? "mods";
        var fileName = Path.GetFileName(known.Path);

        // A conferência de loader é local (lê o jar do blob store), e o arquivo
        // pode ter vindo de outro modpack, com outra build de loader fixada.
        await using (var stored = await blobStore.OpenAsync(known.Sha256, ct))
        {
            var (incompativel, _) =
                await InspectJarAsync(item, fileName, folder, stored, version.LoaderVersion, ct);
            if (incompativel is not null)
                return incompativel;
        }

        var failure = await AttachAsync(version, item, new IngestedFile(
            known.Path, known.Sha256, known.SizeBytes, known.Side, fileId, known.IconUrl, deps), unsaved, ct);

        return failure ?? ResolveOutcome.Ok(deps);
    }

    /// <summary>
    ///     Baixa, confere e anexa. Devolve null em sucesso, ou o desfecho que
    ///     interrompeu (falha ou pendência).
    /// </summary>
    private async Task<ResolveOutcome?> DownloadAndAttachAsync(
        ModpackVersion version,
        ModIngestionItem item,
        ModResolution.Resolved resolved,
        List<ModpackFile> unsaved,
        CancellationToken ct)
    {
        // Já temos exatamente ESTE arquivo nesta versão? Então não há o que baixar.
        //
        // O OriginReference guarda o id da release fixada na origem, que é a
        // identidade do arquivo lá — se ele bate, os bytes são os mesmos e
        // buscá-los serviria só para descartá-los depois de hashear.
        if (version.Files.Any(f =>
                string.Equals(f.ProjectSlug, item.ProjectId, StringComparison.OrdinalIgnoreCase)
                && f.OriginReference == resolved.VersionId))
        {
            return null;
        }

        var deps = RequiredOf(resolved);
        var path = $"{resolved.Folder}/{resolved.FileName}";

        // Degrau do DISCO: a origem disse qual release é (o "mais recente"
        // compatível só ela sabe), mas os bytes podem já estar aqui, ingeridos
        // por outro modpack. Mesmo id na origem = mesmo conteúdo; baixar de novo
        // só serviria para descobrir o hash que já temos.
        if (await repository.FindIngestedFileAsync(item.Origin, resolved.VersionId, ct) is { } known
            && await blobStore.ExistsAsync(known.Sha256, ct))
        {
            await using var local = await blobStore.OpenAsync(known.Sha256, ct);
            var (localIncompat, localInfo) =
                await InspectJarAsync(item, resolved.FileName, resolved.Folder, local, version.LoaderVersion, ct);
            if (localIncompat is not null)
                return localIncompat;

            return await AttachAsync(version, item, new IngestedFile(
                path, known.Sha256, known.SizeBytes,
                resolved.Side ?? localInfo?.DeclaredSide ?? known.Side,
                resolved.VersionId, resolved.IconUrl ?? known.IconUrl, deps), unsaved, ct);
        }

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

            // A exigência de loader do mod não está em API nenhuma — só dentro
            // do jar. Como ele já passou por aqui, conferir sai de graça, e é a
            // diferença entre um aviso no painel e um crash no arranque.
            var (incompativel, jarInfo) =
                await InspectJarAsync(item, resolved.FileName, resolved.Folder, stored, version.LoaderVersion, ct);

            if (incompativel is not null)
                return incompativel;

            // Ordem de confiança do lado: a origem primeiro (o Modrinth
            // declara), depois o próprio JAR (o Fabric padroniza
            // "environment"), e só então o que a ingestão pediu — que para
            // um pack CurseForge é sempre "os dois", porque o manifest não
            // diz nada. O NeoForge não declara lado em lugar nenhum, e aí
            // sobra o server pack do autor ou o admin.
            return await AttachAsync(version, item, new IngestedFile(
                path, sha256, stored.Length,
                resolved.Side ?? jarInfo?.DeclaredSide ?? item.Side,
                resolved.VersionId, resolved.IconUrl, deps), unsaved, ct);
        }
        catch (HttpRequestException ex)
        {
            LogDownloadError(ex, resolved.DownloadUrl.ToString());
            return ResolveOutcome.Fail($"{resolved.FileName} (falha no download)");
        }
    }

    /// <summary>
    ///     Anexa à versão um arquivo cujos bytes já estão no blob store — venha
    ///     ele de um download agora ou de uma ingestão anterior. Um lugar só para
    ///     as regras de "um .jar por mod" e de conflito de caminho, que valem
    ///     igual para os dois. Devolve null em sucesso.
    /// </summary>
    private async Task<ResolveOutcome?> AttachAsync(
        ModpackVersion version,
        ModIngestionItem item,
        IngestedFile ingested,
        List<ModpackFile> unsaved,
        CancellationToken ct)
    {
        // Mesmo mod, mesmo conteúdo já presente? Nada a fazer — evita
        // remover e re-adicionar a mesma linha numa re-ingestão.
        if (version.Files.Any(f =>
                string.Equals(f.ProjectSlug, item.ProjectId, StringComparison.OrdinalIgnoreCase)
                && f.Sha256 == ingested.Sha256))
            return null;

        // Conflito raro: outro mod já ocupa este caminho. Dois arquivos no
        // mesmo path não podem coexistir na instância.
        if (version.Files.Any(f =>
                f.Path.Equals(ingested.Path, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(f.ProjectSlug, item.ProjectId, StringComparison.OrdinalIgnoreCase)))
            return ResolveOutcome.Fail($"{Path.GetFileName(ingested.Path)} (conflito de caminho com outro mod)");

        var file = new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = item.ProjectId, // identidade estável do mod
            Path = ingested.Path,
            Sha256 = ingested.Sha256,
            SizeBytes = ingested.SizeBytes,
            Side = ingested.Side,
            Optional = item.Optional,
            Origin = item.Origin,
            OriginReference = ingested.OriginReference, // id da versão fixada (base do check de updates)
            IconUrl = ingested.IconUrl, // cosmético: exibido na grade de mods
            RequiredDependencies = ModpackFile.JoinDependencies(ingested.Dependencies)
        };

        // Mesmo mod em outra versão do .jar (jei-1.2.0 → jei-1.5)? Substitui,
        // nunca acumula dois arquivos do mesmo mod em mods/. O UpsertFile
        // devolve o ID do arquivo trocado para apagarmos a linha antiga —
        // o UpdateVersionAsync final (Update num grafo destacado) não apaga
        // filhos removidos da coleção sozinho.
        var replacedId = version.UpsertFile(file);
        if (replacedId is { } oldId)
            await repository.RemoveFileAsync(version.Id, oldId, ct);

        unsaved.Add(file);
        return null;
    }

    /// <summary>
    ///     Só as REQUERIDAS entram na fila. Embedded já vem dentro do jar;
    ///     optional é escolha do usuário; incompatible nunca se puxa.
    /// </summary>
    private static IReadOnlyList<string> RequiredOf(ModResolution.Resolved resolved) =>
        [.. resolved.Dependencies.Where(d => d.Kind is ModDependencyKind.Required).Select(d => d.ProjectId)];

    /// <summary>
    ///     Confere a versão do loader exigida pelo jar contra a fixada na versão.
    ///     Devolve a pendência quando é incompatível, ou null quando pode passar
    ///     — inclusive quando não deu para ler nada, porque uma recusa errada
    ///     bloqueia um mod que funcionaria.
    /// </summary>
    /// <summary>
    ///     Abre o jar uma vez e devolve as duas coisas que ele sabe: se a
    ///     exigência de loader bate, e que lado ele declara.
    ///     Uma leitura só — o arquivo já está aberto, e abrir de novo para o
    ///     lado custaria o dobro em centenas de mods.
    /// </summary>
    private async Task<(ResolveOutcome? Incompativel, ModJarInfo? Info)> InspectJarAsync(
        ModIngestionItem item,
        string fileName,
        string folder,
        Stream jar,
        string loaderVersion,
        CancellationToken ct)
    {
        var info = await jarInspector.InspectAsync(jar, ct);
        if (info?.RequiredLoaderRange is not { Length: > 0 } exigido)
            return (null, info);

        if (LoaderVersionRange.IsSatisfied(exigido, loaderVersion))
            return (null, info);

        LogLoaderMismatch(fileName, exigido, loaderVersion);

        return (ResolveOutcome.Postpone(new PendingMod
        {
            ModpackVersionId = Guid.Empty, // preenchido pelo UpsertPending
            ProjectSlug = item.ProjectId,
            DisplayName = fileName,
            Origin = item.Origin,
            FileId = item.FileId,
            Side = item.Side,
            Reason = PendingModReason.NoCompatibleFile,
            Detail = $"Exige loader {exigido}; esta versão usa {loaderVersion}.",
            Folder = folder
        }), info);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Versão {VersionId} não encontrada para ingestão.")]
    private partial void LogVersionMissing(Guid versionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transição inválida na versão {VersionId}: {Reason}")]
    private partial void LogInvalidTransition(Guid versionId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro inesperado na ingestão da versão {VersionId}.")]
    private partial void LogUnexpected(Exception ex, Guid versionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao baixar {Url}.")]
    private partial void LogDownloadError(Exception ex, string url);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{FileName} exige loader {Required}, mas a versão usa {Actual}.")]
    private partial void LogLoaderMismatch(string fileName, string required, string actual);

    /// <summary>Um arquivo com os bytes já no blob store, pronto para entrar na versão.</summary>
    private sealed record IngestedFile(
        string Path,
        string Sha256,
        long SizeBytes,
        FileSide Side,
        string OriginReference,
        string? IconUrl,
        IReadOnlyList<string> Dependencies);

    /// <summary>Contadores do progresso, mutáveis entre o laço e o relatório.</summary>
    private sealed class Counters
    {
        public int Deps;
        public int Done;
    }

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
