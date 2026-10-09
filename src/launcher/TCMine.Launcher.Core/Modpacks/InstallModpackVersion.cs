using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Faz o disco do jogador convergir para o manifesto de uma versão.
///     O modelo é declarativo: o manifesto descreve o estado final, o
///     <see cref="ManifestDiffer" /> diz a diferença, e este caso de uso executa.
///     Instalar e atualizar são a MESMA operação — não há caminho separado para
///     "primeira vez", porque um diff contra uma instância vazia já é a
///     instalação completa.
/// </summary>
public sealed partial class InstallModpackVersion(
    IServerConnection connection,
    IContentStore content,
    IBlobDownloader downloader,
    IInstanceStore instances,
    ILogger<InstallModpackVersion>? logger = null) : IInstanceInstaller
{
    // TEMPORÁRIO — linha de base da refatoração (docs/BASELINE.md, L5 e L6).
    // Sai no fim da fase 8. Opcional de propósito: os testes constroem este
    // caso de uso à mão, e medição não é motivo para mexer neles.
    private readonly ILogger _logger = logger ?? NullLogger<InstallModpackVersion>.Instance;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Instalação de modpack ({Kind}): {Modpack} {Version} — total {TotalMs} ms; "
                  + "plano {PlanMs} ms; download {DownloadMs} ms ({DownloadFiles} arquivos, {DownloadBytes} bytes); "
                  + "aplicação {ApplyMs} ms ({ApplyFiles} arquivos); fecho {FinishMs} ms.")]
    private partial void LogInstallTimings(
        string kind, string modpack, string version, long totalMs,
        long planMs, long downloadMs, int downloadFiles, long downloadBytes,
        long applyMs, int applyFiles, long finishMs);

    private static long ElapsedMs(long from, long to) =>
        (long)Stopwatch.GetElapsedTime(from, to).TotalMilliseconds;

    // 2 acrescentou MinecraftVersion, Loader e LoaderVersion, sem os quais não
    // se abre o jogo offline. Schema 1 continua legível — só não dá para jogar
    // até reinstalar, e o caso de uso do launch explica isso.
    private const int ManifestSchema = 2;

    /// <summary>
    ///     Instala a versão que o servidor considera a atual.
    ///     Pega o id de uma consulta e o manifesto de outra, mesmo que a primeira
    ///     já traga os arquivos: o que a lista de versões carrega é decisão do
    ///     repositório do servidor, e uma otimização lá — deixar de incluir os
    ///     arquivos na listagem — faria a instalação virar silenciosamente uma
    ///     pasta vazia. A chamada extra acontece uma vez por instalação.
    /// </summary>
    public async Task<InstallResult> InstallLatestAsync(
        Uri serverUrl,
        ModpackDto modpack,
        InstanceKey? target,
        ReleaseChannel channel,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var latest = await connection.GetLatestVersionAsync(modpack.Id, channel, ct);

        if (latest is null)
        {
            // Resposta legítima: o administrador criou o pack e ainda não
            // publicou naquele canal. Dizer qual canal importa — um pack pode ter
            // estáveis e nenhuma alpha, e "não tem versão" sozinho mandaria
            // procurar problema onde não há.
            return InstallResult.Failure(channel is ReleaseChannel.Alpha
                ? $"{modpack.Name} ainda não tem nenhuma versão alpha publicada."
                : $"{modpack.Name} ainda não tem uma versão publicada para instalar.");
        }

        return await HandleAsync(serverUrl, modpack, latest.Id, target, progress, ct);
    }

    /// <summary>
    ///     Instala uma versão numa instância.
    ///     O <paramref name="target" /> é quem decide entre atualizar e duplicar,
    ///     e essa decisão é de quem chama — não daqui. Passar uma instância
    ///     existente reescreve os mods dela e PRESERVA o mundo, porque o diff
    ///     corre contra o manifesto que lá está; passar nulo cria uma instalação
    ///     nova, com mundo próprio.
    ///     Antes a chave saía do par (modpack, versão), e por isso atualizar
    ///     fabricava sempre uma pasta nova e deixava o mundo para trás na antiga.
    /// </summary>
    public async Task<InstallResult> HandleAsync(
        Uri serverUrl,
        ModpackDto modpack,
        Guid versionId,
        InstanceKey? target,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var key = target ?? InstanceKey.New();
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            progress?.Report(InstallProgress.Planning);

            var manifest = await connection.GetModpackVersionAsync(versionId, ct);

            // ---------------------------------------------------------------
            // GUARD CRÍTICO. O conjunto local vem do MANIFESTO que gravamos, e
            // NUNCA de uma varredura da pasta. Uma varredura acharia saves/,
            // screenshots/ e options.txt — que não estão no manifesto do pack e
            // portanto entrariam em ToDelete. O primeiro update apagaria os
            // mundos dos jogadores.
            // ---------------------------------------------------------------
            var local = await instances.ReadManifestAsync(key, ct);
            var arquivosLocais = local?.ManagedFiles ?? new Dictionary<string, string>();

            var noStore = await content.ListHashesAsync(ct);

            var plano = ManifestDiffer.Plan(key, manifest, arquivosLocais, noStore, includeOptional: false);

            var plannedAt = Stopwatch.GetTimestamp();
            await DownloadAsync(serverUrl, plano, progress, ct);
            var downloadedAt = Stopwatch.GetTimestamp();
            await MaterializarAsync(key, plano, progress, ct);
            var appliedAt = Stopwatch.GetTimestamp();

            if (plano.ToDelete.Count > 0)
            {
                progress?.Report(InstallProgress.Cleaning);
                await instances.DeleteFilesAsync(key, plano.ToDelete, ct);
            }

            var installed = new InstanceManifest
            {
                Schema = ManifestSchema,
                ModpackId = modpack.Id,
                ModpackVersionId = versionId,
                ModpackName = modpack.Name,
                Version = manifest.Version,
                InstalledAt = DateTimeOffset.UtcNow,

                MinecraftVersion = modpack.MinecraftVersion,
                Loader = modpack.Loader,
                LoaderVersion = manifest.LoaderVersion,

                // O manifesto gravado descreve o ESTADO FINAL desejado, e não o
                // que esta execução mexeu: é contra ele que o próximo update vai
                // diferenciar, e um registro parcial faria o diff seguinte achar
                // que os arquivos intocados são lixo.
                ManagedFiles = manifest.Files
                    .Where(f => f.Side is not FileSide.ServerOnly && !f.Optional)
                    .ToDictionary(f => f.Path, f => f.Sha256),

                MemoryMb = local?.MemoryMb ?? manifest.RecommendedMemoryMb
            };

            await instances.WriteManifestAsync(key, installed, ct);

            progress?.Report(InstallProgress.Done);

            // Em variáveis locais antes do log: CA1873 cobra argumento barato.
            var finishedAt = Stopwatch.GetTimestamp();
            var kind = local is null ? "instalação" : "atualização";

            // O download deduplica por hash (ver DownloadAsync); a contagem aqui
            // segue a mesma regra, para o número bater com os pedidos feitos.
            var unicos = plano.ToDownload
                .DistinctBy(f => f.Sha256, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var downloadFiles = unicos.Length;
            var downloadBytes = unicos.Sum(f => f.SizeBytes);
            var applyFiles = plano.ToMaterialize.Count;

            var totalMs = ElapsedMs(startedAt, finishedAt);
            var planMs = ElapsedMs(startedAt, plannedAt);
            var downloadMs = ElapsedMs(plannedAt, downloadedAt);
            var applyMs = ElapsedMs(downloadedAt, appliedAt);
            var finishMs = ElapsedMs(appliedAt, finishedAt);
            var modpackName = modpack.Name;
            var version = manifest.Version;

            LogInstallTimings(
                kind, modpackName, version, totalMs,
                planMs, downloadMs, downloadFiles, downloadBytes,
                applyMs, applyFiles, finishMs);

            return InstallResult.Success(key, installed);
        }
        catch (OperationCanceledException)
        {
            // Cancelar é do jogador. A instância fica pela metade, e o próximo
            // diff conserta — é justamente o que o modelo declarativo garante.
            throw;
        }
        catch (Exception ex)
        {
            return InstallResult.Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Downloads simultâneos. Em série, um pack como o ATM10 (centenas de mods e
    ///     milhares de configs pequenos) pagava a latência de cada pedido um atrás
    ///     do outro, e a rede ficava ociosa entre eles — lento mesmo em rede local.
    ///     Seis e não mais: o servidor aceita oito transferências por cliente
    ///     (<c>RateLimitPolicies.BlobConcurrency</c>), e ficar abaixo deixa folga
    ///     para outra tela do launcher (ícones, Java) não cair na fila.
    /// </summary>
    public const int ParallelDownloads = 6;

    private async Task DownloadAsync(
        Uri serverUrl, SyncPlan plano, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (plano.ToDownload.Count is 0)
            return;

        // Um hash por download: o mesmo conteúdo em dois caminhos (configs
        // idênticos, arquivos vazios) entra duas vezes no plano, e em paralelo as
        // duas cópias gravariam o mesmo temporário do store ao mesmo tempo.
        // Os maiores primeiro, para um jar de centenas de megabytes não ser o
        // último a começar e deixar o fim do download com uma conexão só.
        var fila = plano.ToDownload
            .DistinctBy(f => f.Sha256, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(f => f.SizeBytes)
            .ToArray();

        long baixados = 0;
        var total = fila.Sum(f => f.SizeBytes);

        progress?.Report(InstallProgress.Downloading(0, total, null));

        await Parallel.ForEachAsync(
            fila,
            new ParallelOptions { MaxDegreeOfParallelism = ParallelDownloads, CancellationToken = ct },
            async (file, token) =>
            {
                await using (var source = await downloader.OpenAsync(serverUrl, file.Sha256, token))
                {
                    // O store recalcula o hash enquanto grava e rejeita se não
                    // bater: o arquivo pode ter chegado corrompido ou adulterado.
                    await content.AddAsync(file.Sha256, source, token);
                }

                var feitos = Interlocked.Add(ref baixados, file.SizeBytes);
                progress?.Report(InstallProgress.Downloading(feitos, total, file.Path));
            });

        progress?.Report(InstallProgress.Downloading(total, total, null));
    }

    private async Task MaterializarAsync(
        InstanceKey key, SyncPlan plano, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var raiz = instances.PathFor(key);
        var feitos = 0;

        foreach (var file in plano.ToMaterialize)
        {
            progress?.Report(InstallProgress.Materializing(feitos, plano.ToMaterialize.Count, file.Path));

            await content.MaterializeAsync(
                file.Sha256,
                Path.Combine(raiz, file.Path),
                InstanceLayout.CanHardLink(file.Path),
                ct);

            feitos++;
        }
    }
}

/// <summary>
///     O que saiu da instalação.
///     A <see cref="Key" /> vem junto porque com identidade própria a instância
///     deixou de ser deduzível do par (modpack, versão): quem instalou precisa
///     dela para a marcar como ativa, para atualizar depois, ou apenas para saber
///     em que pasta mexeu.
/// </summary>
public sealed record InstallResult(bool Succeeded, InstanceKey? Key, InstanceManifest? Instance, string? Error)
{
    public static InstallResult Success(InstanceKey key, InstanceManifest instance) =>
        new(true, key, instance, null);

    public static InstallResult Failure(string error) => new(false, null, null, error);
}

/// <summary>
///     O que mostrar enquanto instala.
///     Bytes na fase de download e contagem de arquivos na de materialização,
///     porque são grandezas diferentes: baixar é limitado pela rede e materializar
///     pelo disco, e uma barra só para as duas mentiria em uma delas.
/// </summary>
public sealed record InstallProgress(
    InstallPhase Phase,
    long BytesDone = 0,
    long BytesTotal = 0,
    int FilesDone = 0,
    int FilesTotal = 0,
    string? CurrentFile = null)
{
    public static readonly InstallProgress BackingUp = new(InstallPhase.BackingUp);
    public static readonly InstallProgress Planning = new(InstallPhase.Planning);
    public static readonly InstallProgress Cleaning = new(InstallPhase.Cleaning);
    public static readonly InstallProgress Done = new(InstallPhase.Done);

    public static InstallProgress Downloading(long done, long total, string? file) =>
        new(InstallPhase.Downloading, done, total, CurrentFile: file);

    public static InstallProgress Materializing(int done, int total, string? file) =>
        new(InstallPhase.Materializing, FilesDone: done, FilesTotal: total, CurrentFile: file);

    /// <summary>
    ///     O texto da fase — nulo em Done, que vira a mensagem de sucesso de quem
    ///     chamou. No Core porque a tela de instâncias e o "Entrar" do servidor
    ///     mostram a mesma instalação, e dois textos para a mesma fase divergem.
    /// </summary>
    public string? Label => Phase switch
    {
        InstallPhase.BackingUp => "Copiando o mundo…",
        InstallPhase.Downloading => "Baixando arquivos…",
        InstallPhase.Materializing => "Instalando…",
        InstallPhase.Cleaning => "Limpando o que sobrou…",
        InstallPhase.Done => null,
        _ => "Preparando…"
    };

    /// <summary>Fração de 0 a 1, ou nulo quando não há como saber.</summary>
    public double? Fraction => Phase switch
    {
        InstallPhase.Downloading when BytesTotal > 0 => (double)BytesDone / BytesTotal,
        InstallPhase.Materializing when FilesTotal > 0 => (double)FilesDone / FilesTotal,
        InstallPhase.Done => 1,
        _ => null
    };
}

public enum InstallPhase
{
    /// <summary>Só acontece numa atualização, e só quando há mundo.</summary>
    BackingUp,

    Planning,
    Downloading,
    Materializing,
    Cleaning,
    Done
}
