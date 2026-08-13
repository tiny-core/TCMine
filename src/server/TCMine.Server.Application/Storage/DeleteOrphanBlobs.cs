using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Storage;

/// <summary>
///     Apaga blobs sem dono, reconferindo tudo no último instante.
///     A varredura que produziu a lista pode ter minutos de idade, e nesse tempo
///     uma importação pode ter passado a referenciar um deles — o store é
///     endereçado por conteúdo, então dois packs diferentes com o mesmo mod
///     compartilham o MESMO blob. Apagar por uma lista velha é como remover um
///     arquivo porque ele estava livre quando você olhou.
/// </summary>
public sealed class DeleteOrphanBlobs(
    IBlobJanitor janitor,
    IModpackRepository repository,
    IJobProgressReporter progress)
{
    /// <summary>
    ///     <paramref name="jobId" /> liga o trabalho ao acompanhamento global.
    ///     Apagar centenas de arquivos leva tempo, e uma janela que fecha sem
    ///     dizer nada deixa o admin sem saber se está andando, travou, ou já
    ///     acabou.
    /// </summary>
    public async Task<Result<DeletionSummary>> HandleAsync(
        IReadOnlyCollection<string> hashes, CancellationToken ct, Guid jobId = default)
    {
        void Report(string step, int done, int total)
        {
            if (jobId != default)
                progress.Report(jobId, new JobProgress("Limpando o content store", step, done, total));
        }

        Report("Reconferindo referências…", 0, 0);

        if (hashes.Count is 0)
        {
            progress.Complete(jobId, "Nada selecionado.");
            return Result<DeletionSummary>.Fail("Nada selecionado.");
        }

        // Reconfere AGORA, não confia na varredura.
        var referenced = await repository.ListReferencedHashesAsync(ct);
        var cutoff = DateTimeOffset.UtcNow - ScanStorage.MinimumAge;

        var stillOnDisk = new Dictionary<string, StoredBlob>(StringComparer.OrdinalIgnoreCase);
        await foreach (var blob in janitor.EnumerateAsync(ct))
        {
            if (hashes.Contains(blob.Sha256))
                stillOnDisk[blob.Sha256] = blob;
        }

        var deleted = 0;
        var skipped = 0;
        long freed = 0;

        foreach (var hash in hashes)
        {
            Report($"{deleted + skipped + 1} de {hashes.Count}", deleted + skipped, hashes.Count);

            if (!stillOnDisk.TryGetValue(hash, out var blob))
            {
                skipped++; // sumiu entre a varredura e agora
                continue;
            }

            // Passou a ser referenciado, ou é novo demais: não se apaga.
            if (referenced.Contains(hash) || blob.CreatedAt > cutoff)
            {
                skipped++;
                continue;
            }

            if (await janitor.DeleteAsync(hash, ct))
            {
                deleted++;
                freed += blob.SizeBytes;
            }
            else
                skipped++;
        }

        progress.Complete(jobId);

        return Result<DeletionSummary>.Success(new DeletionSummary(deleted, skipped, freed));
    }
}

public sealed record DeletionSummary(int Deleted, int Skipped, long FreedBytes);
