using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Reconstrói, a partir do banco, o que falta ingerir numa versão.
///     É o que torna a fila em memória recuperável sem tabela de jobs: o pedido
///     original não precisa sobreviver ao processo, porque dá para deduzi-lo do
///     estado persistido — o que já chegou (Files), o que falhou ou espera
///     (PendingMods) e o que o pack de origem prometia (UpstreamSnapshot).
///     Função pura de propósito: o reparo pedido pelo admin e a recuperação
///     automática do arranque usam exatamente o mesmo cálculo, e duas cópias
///     divergiriam na primeira correção.
/// </summary>
public static class IngestionWorkPlanner
{
    public static List<ModIngestionItem> PlanRetry(ModpackVersion version, Modpack modpack)
    {
        var present = version.Files
            .Where(f => f.ProjectSlug is not null)
            .Select(f => f.ProjectSlug!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new Dictionary<string, ModIngestionItem>(StringComparer.OrdinalIgnoreCase);

        // Pendências que ainda podem resolver. Inclui as Queued (pedidas e nunca
        // tentadas) e exclui DistributionDenied: é decisão do autor do mod, e
        // insistir só gasta chamada de API e frustra o admin.
        foreach (var pending in version.PendingMods
                     .Where(p => p.Reason is not PendingModReason.DistributionDenied))
        {
            items[pending.ProjectSlug] = new ModIngestionItem(
                pending.Origin, pending.ProjectSlug, pending.FileId, pending.Side);
        }

        // Numa versão importada, o snapshot diz o que "deveria existir": pega o
        // que sumiu sem nem ter virado pendência (ingestão interrompida no meio).
        var snapshot = UpstreamSnapshot.FromJson(version.UpstreamSnapshotJson);
        if (snapshot is not null && modpack.UpstreamProvider is { } origin)
        {
            foreach (var (projectId, fileId) in snapshot.Mods)
            {
                if (present.Contains(projectId) || items.ContainsKey(projectId))
                    continue;

                items[projectId] = new ModIngestionItem(origin, projectId, fileId, FileSide.Both);
            }
        }

        return [.. items.Values];
    }
}
