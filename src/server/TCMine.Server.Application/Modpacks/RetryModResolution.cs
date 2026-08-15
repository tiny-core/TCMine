using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Tenta de novo o que não veio: reenfileira SÓ o que falta.
///     Serve aos dois casos — versão que falhou (volta para rascunho) e versão em
///     rascunho com pendências que ainda podem mudar de resultado. O que já foi
///     baixado continua válido (o hash foi conferido), então rebaixar tudo seria
///     desperdício de banda e de cota de API.
///     Pendência por redistribuição negada nunca entra: é decisão do autor, e
///     insistir só gasta chamada e frustra o admin.
/// </summary>
public sealed class RetryModResolution(
    IModpackRepository repository,
    IngestionScheduler scheduler)
{
    public async Task<Result<int>> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<int>.Fail("Versão não encontrada.");

        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack is null)
            return Result<int>.Fail("Modpack não encontrado.");

        if (version.State is ModpackVersionState.Failed)
        {
            version.RetryAfterFailure();
        }
        else if (version.State is not ModpackVersionState.Draft)
        {
            return Result<int>.Fail(
                $"Só é possível tentar de novo numa versão em rascunho ou que falhou. Estado atual: {version.State}.");
        }

        var items = IngestionWorkPlanner.PlanRetry(version, modpack);

        await repository.UpdateVersionAsync(version, ct);

        // Passa pelo agendador para o pedido ficar gravado antes de entrar na
        // fila — vale para o reparo tanto quanto para a ingestão original.
        await scheduler.ScheduleAsync(version, items, ct);

        return Result<int>.Success(items.Count);
    }
}
