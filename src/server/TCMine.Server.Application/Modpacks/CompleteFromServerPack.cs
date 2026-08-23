using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Preenche as pendências de um rascunho com os .jar que vêm dentro do
///     server pack publicado pelo autor.
///     Por que isto resolve o problema: no CurseForge o autor pode proibir que
///     terceiros baixem o .jar pela API — é a razão da maioria das pendências —,
///     mas o server pack que ele mesmo publica é um zip com os arquivos DENTRO.
///     O que a API nega em separado, o autor entrega junto.
///     Só mexe no que falta. Um mod que já entrou pela ingestão não é tocado:
///     dois .jar do mesmo mod na pasta mods/ derrubam o jogo no arranque.
///     Só em rascunho, como toda edição de versão.
/// </summary>
public sealed partial class CompleteFromServerPack(
    IEnumerable<IUpstreamPackSource> sources,
    IModpackRepository repository,
    IBlobStore blobStore,
    IJobProgressReporter progress,
    ILogger<CompleteFromServerPack> logger)
{
    private readonly ILogger<CompleteFromServerPack> _logger = logger;

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao completar a versão {VersionId} pelo server pack.")]
    private partial void LogFalha(Exception ex, Guid versionId);

    public async Task<Result<ServerPackFillResult>> HandleAsync(
        Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<ServerPackFillResult>.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result<ServerPackFillResult>.Fail("Só é possível completar uma versão em rascunho.");

        if (version.UpstreamServerPackFileId is not { Length: > 0 } serverPackFileId)
            return Result<ServerPackFillResult>.Fail("Esta versão não tem um server pack na origem.");

        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack?.UpstreamProjectId is not { Length: > 0 } projectId)
            return Result<ServerPackFillResult>.Fail("O modpack não guarda a origem de onde foi importado.");

        var pendencias = version.ManualUploads;
        if (pendencias.Count is 0)
            return Result<ServerPackFillResult>.Success(new ServerPackFillResult(0, 0));

        var source = await OrigemAsync(modpack.UpstreamProvider, ct);
        if (source is null)
            return Result<ServerPackFillResult>.Fail("A origem do pack não está configurada.");

        void Passo(string texto, int feitos, int total) =>
            progress.Report(versionId, new JobProgress("Completando pelo server pack", texto, feitos, total));

        try
        {
            Passo("Baixando o server pack…", 0, pendencias.Count);

            await using var pack = await source.OpenServerPackAsync(projectId, serverPackFileId, ct);
            if (pack is null)
            {
                progress.Complete(versionId, "O server pack não pôde ser baixado.");
                return Result<ServerPackFillResult>.Fail(
                    "O server pack não pôde ser baixado — o autor também bloqueou a distribuição dele.");
            }

            // A pendência guarda o ID da release; o zip traz nomes de arquivo.
            // Esta consulta é a ponte entre os dois, e é uma só para a lista
            // inteira.
            var nomes = await source.GetFileNamesAsync(
                [.. pendencias.Select(p => p.FileId).OfType<string>()], ct);

            var preenchidos = 0;
            var feitos = 0;

            foreach (var pendencia in pendencias)
            {
                ct.ThrowIfCancellationRequested();

                Passo(pendencia.DisplayName, feitos++, pendencias.Count);

                if (pendencia.FileId is not { Length: > 0 } fileId
                    || !nomes.TryGetValue(fileId, out var nomeDoArquivo)
                    || !pack.ModFileNames.Contains(nomeDoArquivo))
                {
                    continue;
                }

                await using var conteudo = pack.OpenMod(nomeDoArquivo);

                // Sem hash esperado: o zip é a fonte, não há um segundo valor
                // com que confrontar. O blob store devolve o hash real.
                var sha = await blobStore.PutAsync(conteudo, null, "application/java-archive", ct);

                var caminho = $"mods/{nomeDoArquivo}";
                var arquivo = new ModpackFile
                {
                    ModpackVersionId = version.Id,
                    Path = caminho,
                    Sha256 = sha,
                    SizeBytes = await TamanhoAsync(blobStore, sha, ct),
                    Side = pendencia.Side,
                    Origin = pendencia.Origin,

                    // O slug da pendência, e não um sintético: é ele que dá a
                    // identidade do mod, e é por ele que uma atualização futura
                    // substitui este arquivo em vez de acumular outro .jar.
                    ProjectSlug = pendencia.ProjectSlug,
                    OriginReference = fileId
                };

                version.UpsertFile(arquivo);

                if (version.ResolvePending(pendencia.ProjectSlug) is { } resolvida)
                    await repository.RemovePendingAsync(version.Id, resolvida, ct);

                preenchidos++;
            }

            await repository.UpdateVersionAsync(version, ct);
            progress.Complete(versionId);

            return Result<ServerPackFillResult>.Success(
                new ServerPackFillResult(preenchidos, pendencias.Count - preenchidos));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Rede, disco cheio, zip corrompido: nada disso é regra de negócio,
            // mas o admin precisa saber o motivo em vez de ver a tela travada.
            LogFalha(ex, versionId);
            progress.Complete(versionId, ex.Message);
            return Result<ServerPackFillResult>.Fail($"Falha ao ler o server pack: {ex.Message}");
        }
    }

    private async Task<IUpstreamPackSource?> OrigemAsync(ModFileOrigin? provider, CancellationToken ct)
    {
        foreach (var candidata in sources.Where(s => provider is null || s.Origin == provider))
        {
            if (await candidata.IsAvailableAsync(ct))
                return candidata;
        }

        return null;
    }

    private static async Task<long> TamanhoAsync(IBlobStore store, string sha, CancellationToken ct)
    {
        await using var stream = await store.OpenAsync(sha, ct);
        return stream.Length;
    }
}

/// <summary>Quantas pendências o server pack resolveu, e quantas sobraram.</summary>
public sealed record ServerPackFillResult(int Filled, int Remaining);
