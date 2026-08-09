using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Transiciona uma versão de Draft para Ready.
/// </summary>
public sealed class PublishModpackVersion(
    IModpackRepository repository,
    IServerHubNotifier notifier,
    OverrideUndoService undo)
{
    /// <summary>
    ///     <paramref name="acceptPending" /> é o "eu sei o que estou fazendo" do
    ///     admin: publicar com mods pendentes gera um pack incompleto, então
    ///     exigimos que a decisão seja explícita em vez de deixar passar calado.
    /// </summary>
    public async Task<Result> HandleAsync(Guid versionId, CancellationToken ct, bool acceptPending = false)
    {
        var version = await repository.GetVersionAsync(versionId, ct);

        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.HasPendingMods && !acceptPending)
        {
            return Result.Fail(
                $"{version.PendingMods.Count} mod(s) pendentes de upload manual. "
                + "Envie os arquivos ou confirme a publicação sem eles.");
        }

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

        // ...depois do UpdateVersionAsync + notifier, no ramo de sucesso:
        undo.Clear(versionId);

        return Result.Success();
    }
}
