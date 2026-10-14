using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Devolve a ligação com o pack do autor às versões criadas antes de a
///     criação de versão passar a copiá-la. Até ali, a primeira versão própria
///     depois de uma importação herdava os mods mas esquecia de que release do
///     autor tinha vindo — e ficava sem verificação nem atualização.
///     Roda no arranque e é idempotente: depois da primeira passada, toda
///     versão que descende de uma importada já tem a ligação.
/// </summary>
public sealed class BackfillUpstreamLineage(IModpackRepository repository)
{
    /// <summary>Quantas versões ganharam a ligação.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var modpacks = await repository.ListAsync(ct);
        var filled = 0;

        foreach (var modpack in modpacks.Where(m => m.HasUpstream))
        {
            ct.ThrowIfCancellationRequested();

            // Antiga primeiro: a ligação anda para a frente, de versão em versão.
            var versions = (await repository.ListVersionsAsync(modpack.Id, ct)).OrderBy(v => v.Id).ToList();
            ModpackVersion? lastWithLineage = null;

            foreach (var version in versions)
            {
                if (version.HasUpstreamLineage)
                {
                    lastWithLineage = version;
                    continue;
                }

                if (lastWithLineage is null || !UpstreamLineage.LooksInherited(version, lastWithLineage))
                    continue;

                await repository.SetUpstreamLineageAsync(version.Id, lastWithLineage, ct);
                version.InheritUpstreamFrom(lastWithLineage);
                lastWithLineage = version;
                filled++;
            }
        }

        return filled;
    }
}

/// <summary>Regras puras sobre a descendência de uma versão em relação ao pack do autor.</summary>
public static class UpstreamLineage
{
    /// <summary>
    ///     Se <paramref name="version" /> parece ter sido criada copiando
    ///     <paramref name="ancestor" />: tem pelo menos metade dos mods do
    ///     retrato do autor. Não dá para saber com certeza — a criação não
    ///     registrava a origem —, e o erro que importa evitar é ligar ao autor
    ///     uma versão criada VAZIA: o merge leria cada mod do autor como "o admin
    ///     tirou" e recusaria as atualizações dele. Metade deixa passar a versão
    ///     herdada e podada, e barra a vazia ou refeita do zero.
    /// </summary>
    public static bool LooksInherited(ModpackVersion version, ModpackVersion ancestor)
    {
        if (UpstreamSnapshot.FromJson(ancestor.UpstreamSnapshotJson) is not { Mods.Count: > 0 } snapshot)
            return false;

        var ours = version.Files
            .Where(f => f.ProjectSlug is not null)
            .Select(f => f.ProjectSlug!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var kept = snapshot.Mods.Keys.Count(ours.Contains);
        return kept * 2 >= snapshot.Mods.Count;
    }
}
