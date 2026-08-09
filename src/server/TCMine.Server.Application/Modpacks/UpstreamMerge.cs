using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Merge de três vias entre o pack como veio da origem (base), o pack novo
///     lá fora (theirs) e o estado atual da versão aqui (ours).
///     Função PURA, sem I/O — é isto que a torna testável e é o mesmo desenho do
///     <c>ManifestDiffer.Plan</c> do launcher.
///     O problema que resolve: atualizar um pack importado sem passar por cima do
///     trabalho do admin. Sem a base não dá para distinguir "o autor mexeu neste
///     mod" de "o admin mexeu neste mod" — as duas coisas parecem "diferente do
///     que está lá fora", e o resultado seria desfazer customização a cada
///     atualização.
/// </summary>
public static class UpstreamMerge
{
    public static UpstreamMergePlan Plan(
        UpstreamSnapshot baseSnapshot,
        IReadOnlyDictionary<string, string> theirMods,
        IReadOnlyList<ModpackFile> ourFiles)
    {
        var ourMods = ourFiles
            .Where(f => f.Origin is not ModFileOrigin.Override && f.ProjectSlug is not null)
            .ToDictionary(f => f.ProjectSlug!, f => f.OriginReference ?? "", StringComparer.OrdinalIgnoreCase);

        var add = new List<UpstreamModChange>();
        var update = new List<UpstreamModChange>();
        var remove = new List<UpstreamModChange>();
        var conflicts = new List<UpstreamModConflict>();
        var kept = new List<string>();

        foreach (var (projectId, theirFileId) in theirMods)
        {
            var inBase = baseSnapshot.Mods.TryGetValue(projectId, out var baseFileId);
            var inOurs = ourMods.TryGetValue(projectId, out var ourFileId);
            var name = NameOf(baseSnapshot, projectId);

            switch (inBase, inOurs)
            {
                // O autor adicionou um mod novo, e não temos nada com esse nome.
                case (false, false):
                    add.Add(new UpstreamModChange(projectId, name, null, theirFileId));
                    break;

                // Já existia e ninguém aqui mexeu: aplica a atualização do autor.
                case (true, true) when ourFileId == baseFileId && theirFileId != baseFileId:
                    update.Add(new UpstreamModChange(projectId, name, ourFileId, theirFileId));
                    break;

                // Os dois mexeram no mesmo mod: só o admin decide.
                case (true, true) when ourFileId != baseFileId && theirFileId != baseFileId
                                       && ourFileId != theirFileId:
                    conflicts.Add(new UpstreamModConflict(
                        projectId, name, baseFileId, ourFileId, theirFileId,
                        UpstreamConflictKind.BothChanged));
                    break;

                // O admin apagou um mod que o autor manteve (e mudou ou não).
                case (true, false):
                    conflicts.Add(new UpstreamModConflict(
                        projectId, name, baseFileId, null, theirFileId,
                        UpstreamConflictKind.RemovedHereKeptThere));
                    break;

                // O admin adicionou por conta própria um mod que agora entrou no
                // pack: nada a fazer, o dele já está lá.
                case (false, true):
                    kept.Add(projectId);
                    break;
            }
        }

        foreach (var (projectId, baseFileId) in baseSnapshot.Mods)
        {
            if (theirMods.ContainsKey(projectId))
                continue;

            var name = NameOf(baseSnapshot, projectId);

            if (!ourMods.TryGetValue(projectId, out var ourFileId))
                continue; // já não está aqui: nada a remover

            if (ourFileId == baseFileId)
                // O autor tirou o mod e o admin não tinha mexido: acompanha.
                remove.Add(new UpstreamModChange(projectId, name, ourFileId, null));
            else
                // O admin tinha trocado a versão deste mod, e o autor o removeu.
                conflicts.Add(new UpstreamModConflict(
                    projectId, name, baseFileId, ourFileId, null,
                    UpstreamConflictKind.ChangedHereRemovedThere));
        }

        // O que o admin acrescentou e nunca esteve no pack fica, sempre.
        kept.AddRange(ourMods.Keys.Where(slug =>
            !baseSnapshot.Mods.ContainsKey(slug) && !theirMods.ContainsKey(slug)));

        return new UpstreamMergePlan(add, update, remove, conflicts, kept);
    }

    private static string NameOf(UpstreamSnapshot snapshot, string projectId) =>
        snapshot.Names.GetValueOrDefault(projectId, projectId);
}

/// <summary>
///     O que a atualização faria. <see cref="Conflicts" /> nunca é aplicado
///     sozinho — na dúvida, o lado do admin prevalece e a decisão fica com ele.
/// </summary>
public sealed record UpstreamMergePlan(
    IReadOnlyList<UpstreamModChange> Add,
    IReadOnlyList<UpstreamModChange> Update,
    IReadOnlyList<UpstreamModChange> Remove,
    IReadOnlyList<UpstreamModConflict> Conflicts,
    IReadOnlyList<string> Kept)
{
    public int TotalChanges => Add.Count + Update.Count + Remove.Count;

    public bool IsEmpty => TotalChanges is 0 && Conflicts.Count is 0;
}

public sealed record UpstreamModChange(string ProjectId, string Name, string? FromFileId, string? ToFileId);

public sealed record UpstreamModConflict(
    string ProjectId,
    string Name,
    string? BaseFileId,
    string? OurFileId,
    string? TheirFileId,
    UpstreamConflictKind Kind);

public enum UpstreamConflictKind
{
    /// <summary>Autor e admin trocaram a versão do mesmo mod.</summary>
    BothChanged,

    /// <summary>O admin removeu daqui; o autor continua incluindo.</summary>
    RemovedHereKeptThere,

    /// <summary>O admin trocou a versão; o autor removeu do pack.</summary>
    ChangedHereRemovedThere
}
