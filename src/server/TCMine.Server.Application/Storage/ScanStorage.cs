using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Storage;

/// <summary>
///     Compara o que existe no disco com o que alguma coisa referencia, e diz
///     quanto dá para recuperar.
///     Órfão aqui é OUTRA coisa do órfão da tela de mods: lá é um mod que só vive
///     em versão arquivada (registro no banco, sem uso ativo); aqui é byte no
///     disco que ninguém aponta. Confundir os dois levaria a apagar o que ainda
///     está instalado na máquina de alguém.
/// </summary>
public sealed class ScanStorage(
    IBlobJanitor janitor,
    IModpackRepository repository,
    IJobProgressReporter progress)
{
    /// <summary>
    ///     Idade mínima para um blob ser considerado lixo.
    ///     Esta é a guarda mais importante do módulo: a ingestão grava os
    ///     arquivos no banco em lotes, então existe uma janela em que o blob já
    ///     está no disco e a linha que o referencia ainda não foi gravada.
    ///     Apagar nessa janela corromperia a versão em andamento — o mod some e
    ///     ninguém entende por quê.
    /// </summary>
    public static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public async Task<Result<StorageReport>> HandleAsync(CancellationToken ct, Guid jobId = default)
    {
        void Report(string step, int done)
        {
            // Total desconhecido: só se sabe quantos arquivos há depois de
            // percorrer. Barra indeterminada com contador é honesto; inventar um
            // total seria mentira.
            if (jobId != default)
                progress.Report(jobId, new JobProgress("Varrendo o content store", step, done));
        }

        Report("Lendo as referências…", 0);

        var referenced = await repository.ListReferencedHashesAsync(ct);
        var cutoff = DateTimeOffset.UtcNow - MinimumAge;

        long totalBytes = 0;
        long referencedBytes = 0;
        var totalCount = 0;
        var orphans = new List<OrphanBlob>();

        await foreach (var blob in janitor.EnumerateAsync(ct).ConfigureAwait(false))
        {
            totalCount++;
            totalBytes += blob.SizeBytes;

            // A cada 500: reportar por arquivo inundaria o circuito com dezenas
            // de milhares de renderizações para mover um contador.
            if (totalCount % 500 is 0)
                Report($"{totalCount} arquivos", totalCount);

            if (referenced.Contains(blob.Sha256))
            {
                referencedBytes += blob.SizeBytes;
                continue;
            }

            orphans.Add(new OrphanBlob(blob.Sha256, blob.SizeBytes, blob.CreatedAt, blob.CreatedAt <= cutoff));
        }

        progress.Complete(jobId);

        return Result<StorageReport>.Success(new StorageReport(
            totalCount,
            totalBytes,
            referencedBytes,
            [.. orphans.OrderByDescending(o => o.SizeBytes)]));
    }
}

/// <summary>
///     Retrato do store. <see cref="ReclaimableBytes" /> conta só o que passou da
///     idade mínima — é o número que o admin pode confiar.
/// </summary>
public sealed record StorageReport(
    int TotalCount,
    long TotalBytes,
    long ReferencedBytes,
    IReadOnlyList<OrphanBlob> Orphans)
{
    public long OrphanBytes => Orphans.Sum(o => o.SizeBytes);

    public long ReclaimableBytes => Orphans.Where(o => o.Safe).Sum(o => o.SizeBytes);

    public int ReclaimableCount => Orphans.Count(o => o.Safe);

    /// <summary>Recentes demais para apagar — provavelmente de um trabalho em curso.</summary>
    public int TooRecentCount => Orphans.Count(o => !o.Safe);
}

/// <summary>
///     Um blob sem dono. <paramref name="Safe" /> falso significa "novo demais":
///     pode pertencer a uma ingestão que ainda não gravou a linha.
/// </summary>
public sealed record OrphanBlob(string Sha256, long SizeBytes, DateTimeOffset CreatedAt, bool Safe);
