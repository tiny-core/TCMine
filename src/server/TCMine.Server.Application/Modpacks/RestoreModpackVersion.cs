using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Desfaz o arquivamento de uma versão (Archived → Ready), voltando a
///     oferecê-la a novos clientes.
/// </summary>
public sealed class RestoreModpackVersion(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        try
        {
            version.Restore();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        await repository.UpdateVersionAsync(version, ct);
        return Result.Success();
    }
}
