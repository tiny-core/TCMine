using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     No arranque, retoma as ingestões que o processo anterior deixou pela
///     metade.
///     A fila vive em memória: um deploy, uma queda ou um reinício mata o job mas
///     não o pedido, que ficou gravado como pendência. Antes daqui a versão só
///     era marcada como falha e o admin tinha de clicar "Tentar novamente" — o
///     que funciona, mas exige que alguém esteja olhando. Agora o próprio
///     arranque reenfileira o que falta.
///     O limite de tentativas é o freio: se o que derruba o processo é este job,
///     retomá-lo a cada arranque poria o servidor em ciclo de queda.
/// </summary>
public sealed class RecoverInterruptedIngestions(
    IModpackRepository repository,
    IngestionScheduler scheduler)
{
    /// <summary>Quantas ingestões voltaram para a fila.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var ids = await repository.ListInterruptedIngestionIdsAsync(ct);

        var retomadas = 0;
        foreach (var id in ids)
        {
            if (await RecoverAsync(id, ct))
                retomadas++;
        }

        return retomadas;
    }

    private async Task<bool> RecoverAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return false;

        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack is null)
            return false;

        var items = IngestionWorkPlanner.PlanRetry(version, modpack);

        if (items.Count is 0)
        {
            // Nada a refazer: os downloads terminaram e o processo caiu antes de
            // fechar o estado. Marcar como falha seria mentira — nada falhou.
            // Rascunho é a verdade, e é de onde o admin publica.
            if (version.State is ModpackVersionState.Resolving)
            {
                version.ReturnToDraft();
                await repository.UpdateVersionAsync(version, ct);
            }

            return false;
        }

        if (!version.TryRegisterRecovery())
        {
            version.MarkFailed(
                $"A resolução foi interrompida {ModpackVersion.MaxRecoveryAttempts} vezes seguidas. "
                + "Isso costuma indicar um problema com um dos mods desta versão, e não uma queda "
                + "passageira. Use 'Tentar novamente' para retomar — o que já foi baixado será mantido.");

            await repository.UpdateVersionAsync(version, ct);
            return false;
        }

        // De volta ao rascunho antes de reenfileirar: o worker chama
        // MarkResolving no começo, e essa transição não sai de Resolving. Sem
        // isto o job seria descartado em silêncio pelo próprio serviço.
        if (version.State is ModpackVersionState.Resolving)
            version.ReturnToDraft();

        // O agendador grava as pendências e enfileira — mesmo caminho de uma
        // ingestão pedida pela tela.
        await scheduler.ScheduleAsync(version, items, ct);

        return true;
    }
}
