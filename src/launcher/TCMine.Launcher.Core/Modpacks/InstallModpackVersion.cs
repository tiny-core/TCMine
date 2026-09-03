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
public sealed class InstallModpackVersion(
    IServerConnection connection,
    IContentStore content,
    IBlobDownloader downloader,
    IInstanceStore instances)
{
    private const int ManifestSchema = 1;

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
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var ultima = await connection.GetLatestVersionAsync(modpack.Id, ct);

        if (ultima is null)
        {
            // Resposta legítima: o administrador criou o pack e ainda não
            // publicou. Dizer isso é melhor que uma falha genérica.
            return InstallResult.Failure(
                $"{modpack.Name} ainda não tem uma versão publicada para instalar.");
        }

        return await HandleAsync(serverUrl, modpack, ultima.Id, progress, ct);
    }

    public async Task<InstallResult> HandleAsync(
        Uri serverUrl,
        ModpackDto modpack,
        Guid versionId,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var key = new InstanceKey(modpack.Id, versionId);

        try
        {
            progress?.Report(InstallProgress.Planning);

            var manifesto = await connection.GetModpackVersionAsync(versionId, ct);

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

            var plano = ManifestDiffer.Plan(key, manifesto, arquivosLocais, noStore, includeOptional: false);

            await BaixarAsync(serverUrl, plano, progress, ct);
            await MaterializarAsync(key, plano, progress, ct);

            if (plano.ToDelete.Count > 0)
            {
                progress?.Report(InstallProgress.Cleaning);
                await instances.DeleteFilesAsync(key, plano.ToDelete, ct);
            }

            var instalada = new InstanceManifest
            {
                Schema = ManifestSchema,
                ModpackId = modpack.Id,
                ModpackVersionId = versionId,
                ModpackName = modpack.Name,
                Version = manifesto.Version,
                InstalledAt = DateTimeOffset.UtcNow,

                // O manifesto gravado descreve o ESTADO FINAL desejado, e não o
                // que esta execução mexeu: é contra ele que o próximo update vai
                // diferenciar, e um registro parcial faria o diff seguinte achar
                // que os arquivos intocados são lixo.
                ManagedFiles = manifesto.Files
                    .Where(f => f.Side is not FileSide.ServerOnly && !f.Optional)
                    .ToDictionary(f => f.Path, f => f.Sha256),

                MemoryMb = local?.MemoryMb ?? manifesto.RecommendedMemoryMb
            };

            await instances.WriteManifestAsync(key, instalada, ct);

            progress?.Report(InstallProgress.Done);

            return InstallResult.Success(instalada);
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

    private async Task BaixarAsync(
        Uri serverUrl, SyncPlan plano, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (plano.ToDownload.Count is 0)
            return;

        long baixados = 0;
        var total = plano.BytesToDownload;

        foreach (var arquivo in plano.ToDownload)
        {
            progress?.Report(InstallProgress.Downloading(baixados, total, arquivo.Path));

            await using var origem = await downloader.OpenAsync(serverUrl, arquivo.Sha256, ct);

            // O store recalcula o hash enquanto grava e rejeita se não bater: o
            // arquivo pode ter chegado corrompido ou adulterado no caminho.
            await content.AddAsync(arquivo.Sha256, origem, ct);

            baixados += arquivo.SizeBytes;
        }

        progress?.Report(InstallProgress.Downloading(total, total, null));
    }

    private async Task MaterializarAsync(
        InstanceKey key, SyncPlan plano, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var raiz = instances.PathFor(key);
        var feitos = 0;

        foreach (var arquivo in plano.ToMaterialize)
        {
            progress?.Report(InstallProgress.Materializing(feitos, plano.ToMaterialize.Count, arquivo.Path));

            await content.MaterializeAsync(
                arquivo.Sha256,
                Path.Combine(raiz, arquivo.Path),
                InstanceLayout.CanHardLink(arquivo.Path),
                ct);

            feitos++;
        }
    }
}

public sealed record InstallResult(bool Succeeded, InstanceManifest? Instance, string? Error)
{
    public static InstallResult Success(InstanceManifest instance) => new(true, instance, null);

    public static InstallResult Failure(string error) => new(false, null, error);
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
    public static readonly InstallProgress Planning = new(InstallPhase.Planning);
    public static readonly InstallProgress Cleaning = new(InstallPhase.Cleaning);
    public static readonly InstallProgress Done = new(InstallPhase.Done);

    public static InstallProgress Downloading(long done, long total, string? file) =>
        new(InstallPhase.Downloading, done, total, CurrentFile: file);

    public static InstallProgress Materializing(int done, int total, string? file) =>
        new(InstallPhase.Materializing, FilesDone: done, FilesTotal: total, CurrentFile: file);

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
    Planning,
    Downloading,
    Materializing,
    Cleaning,
    Done
}
