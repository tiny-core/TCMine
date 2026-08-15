using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Ponto único por onde uma ingestão entra na fila.
///     Antes daqui cada chamador falava direto com a IIngestionQueue — dois deles
///     eram componentes de tela. Com a fila em memória, isso significava que o
///     pedido só existia dentro do processo: caindo antes de o worker chegar no
///     item, ninguém sabia que ele tinha sido pedido.
///     Agora o pedido é gravado ANTES de enfileirar, como pendência
///     <see cref="PendingModReason.Queued" />. Ela some sozinha quando o mod
///     resolve e vira outra razão quando falha, então não há estado novo para
///     limpar — e o reparo, que já reconstrói o trabalho a partir das pendências,
///     passa a enxergar também o que nunca chegou a ser tentado.
/// </summary>
public sealed class IngestionScheduler(
    IModpackRepository repository,
    IIngestionQueue queue)
{
    public async Task ScheduleAsync(
        Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
    {
        if (items.Count is 0)
            return;

        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return;

        await ScheduleAsync(version, items, ct);
    }

    /// <summary>
    ///     Sobrecarga para quem já tem a versão em mãos (o caso de uso que
    ///     acabou de criá-la), evitando reler do banco o que está na memória.
    /// </summary>
    public async Task ScheduleAsync(
        ModpackVersion version, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
    {
        if (items.Count is 0)
            return;

        foreach (var item in items)
        {
            // UpsertPending casa por ProjectSlug: se o mod já tinha pendência de
            // outra razão (uma tentativa anterior que falhou), ela vira Queued e
            // não duplica linha.
            version.UpsertPending(new PendingMod
            {
                ModpackVersionId = version.Id,
                ProjectSlug = item.ProjectId,
                DisplayName = item.ProjectId,
                Origin = item.Origin,
                FileId = item.FileId,
                Side = item.Side,
                Reason = PendingModReason.Queued
            });
        }

        // Gravar antes de enfileirar, nunca depois: entre as duas linhas existe
        // uma janela em que o processo pode cair, e é melhor sobrar um pedido
        // registrado (que a recuperação reenfileira) do que faltar.
        await repository.UpdateVersionAsync(version, ct);

        await queue.EnqueueAsync(version.Id, items, ct);
    }
}
