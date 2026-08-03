using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Aposenta uma versão publicada (Ready → Archived): ela some de novas
///     instalações, mas quem já a fixou continua rodando.
/// </summary>
public sealed class ArchiveModpackVersion(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        try
        {
            // A máquina de estados vive no domínio; o caso de uso orquestra.
            version.Archive();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        await repository.UpdateVersionAsync(version, ct);
        return Result.Success();
    }
}
