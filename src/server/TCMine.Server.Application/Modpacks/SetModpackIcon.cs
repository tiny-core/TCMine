using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Define a capa/ícone de um modpack: guarda a imagem no blob store
///     (content-addressed) e aponta o modpack para o hash. Trocar a capa só
///     muda o ponteiro; o blob antigo fica no store (GC de órfãos é à parte).
/// </summary>
public sealed class SetModpackIcon(IModpackRepository repository, IBlobStore blobStore)
{
    public async Task<Result> HandleAsync(
        Guid modpackId, Stream icon, string contentType, CancellationToken ct)
    {
        var modpack = await repository.GetByIdAsync(modpackId, ct);
        if (modpack is null)
            return Result.Fail("Modpack não encontrado.");

        var sha = await blobStore.PutAsync(icon, null, contentType, ct);
        modpack.IconBlobSha256 = sha;
        await repository.UpdateAsync(modpack, ct);

        return Result.Success();
    }
}
