using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Dá identidade aos mods enviados à mão antes de o upload passar a ler o jar.
///     Sem ela eles não aparecem no inventário de mods, e a próxima versão que
///     herdar um deles não sabe que o jar novo do mesmo mod o substitui.
///     Roda no arranque e é idempotente: depois da primeira passada não sobra
///     nada a preencher, e a consulta volta vazia.
/// </summary>
public sealed class BackfillManualModIdentity(IModpackRepository repository, IBlobStore blobs, IModJarInspector jars)
{
    /// <summary>Quantos arquivos ganharam identidade.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var files = await repository.ListManualModsWithoutIdentityAsync(ct);
        var filled = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            // Blob sumido do disco não impede o resto: fica com a identidade
            // pelo nome, que não exige abrir nada.
            string? modId = null;
            if (await blobs.ExistsAsync(file.Sha256, ct))
            {
                await using var jar = await blobs.OpenAsync(file.Sha256, ct);
                modId = (await jars.InspectAsync(jar, ct))?.ModId;
            }

            if (ModpackFile.ManualSlug(file.Path, modId) is not { } slug)
                continue;

            await repository.SetProjectSlugAsync(file.Id, slug, ct);
            filled++;
        }

        return filled;
    }
}
