using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Transiciona uma versão de Draft para Ready.
/// </summary>
public sealed class PublishModpackVersion(
    IModpackRepository repository,
    IServerHubNotifier notifier)
{
    public async Task<Result> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);

        if (version is null)
            return Result.Fail("Versão não encontrada.");

        try
        {
            // A máquina de estados vive no domínio; o caso de uso orquestra.
            version.MarkResolving();
            version.MarkReady();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        await repository.UpdateVersionAsync(version, ct);

        // Avisa os launchers. É otimização — offline reconcilia depois —, então
        // falha aqui não desfaz a publicação.
        await notifier.NotifyModpackVersionPublishedAsync(version.ModpackId, version.Id, ct);

        return Result.Success();
    }
}