using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Apaga um modpack e, em cascata, as suas versões e arquivos. Bloqueia se
///     houver servidores: eles têm containers e mundos apontando para versões
///     deste pack — apagá-lo por baixo deixaria instâncias órfãs. O admin remove
///     os servidores primeiro (onde a parada do container é tratada com cuidado).
/// </summary>
public sealed class DeleteModpack(
    IModpackRepository modpacks,
    IServerRepository servers)
{
    public async Task<Result> HandleAsync(Guid modpackId, CancellationToken ct)
    {
        var modpack = await modpacks.GetByIdAsync(modpackId, ct);
        if (modpack is null)
            return Result.Fail("Modpack não encontrado.");

        var dependents = await servers.ListByModpackAsync(modpackId, ct);
        if (dependents.Count > 0)
        {
            return Result.Fail(
                $"Este modpack tem {dependents.Count} servidor(es). Remova-os antes de apagar o modpack.");
        }

        // Os blobs (jars/overrides) ficam no store — são content-addressed e
        // podem ser partilhados por outros packs. A limpeza de blobs órfãos é
        // um GC separado, não parte de apagar um pack.
        await modpacks.RemoveAsync(modpackId, ct);
        return Result.Success();
    }
}
