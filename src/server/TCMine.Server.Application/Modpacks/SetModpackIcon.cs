using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Define a capa/ícone de um modpack: guarda a imagem no blob store
///     (content-addressed) e aponta o modpack para o hash. Trocar a capa só
///     muda o ponteiro; o blob antigo fica no store (GC de órfãos é à parte).
/// </summary>
public sealed class SetModpackIcon(IModpackRepository repository, IBlobStore blobStore, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(
        Guid modpackId, Stream icon, string contentType, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(modpackId, ModpackAccessPolicy.CanEdit, ct);
        if (!auth.Succeeded)
            return auth;

        // Antes de gravar o blob: um modpack que não existe não deixa imagem
        // órfã no store.
        if (await repository.GetHeaderAsync(modpackId, ct) is null)
            return Result.Fail("Modpack não encontrado.");

        var sha = await blobStore.PutAsync(icon, null, contentType, ct);
        await repository.SetIconAsync(modpackId, sha, ct);

        return Result.Success();
    }
}
