using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Transiciona uma versão de Draft para Ready.
///     Para arquivos manuais, publicar é direto: não há nada a resolver. Quando
///     a ingestão a partir de Modrinth/CurseForge existir, ela passará por
///     Resolving antes — mas o ponto de chegada é o mesmo MarkReady.
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
            // A máquina de estados vive no domínio. O caso de uso orquestra;
            // quem decide se a transição é válida (tem arquivo? está no
            // estado certo?) é a própria entidade.
            version.MarkResolving();
            version.MarkReady();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        await repository.SaveChangesAsync(ct);

        // Avisa os launchers conectados. É otimização — quem estiver offline
        // descobre na próxima reconciliação —, então falha aqui não desfaz a
        // publicação.
        await notifier.NotifyModpackVersionPublishedAsync(version.ModpackId, version.Id, ct);

        return Result.Success();
    }
}