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
///     O plano é só a proposta. Quem decide item a item é o admin, pela
///     <see cref="UpstreamSelection" />; <see cref="Resolve" /> traduz a escolha
///     dele no que o rascunho copia, baixa e descarta.
/// </summary>
public static class UpstreamMerge
{
    /// <summary>
    ///     <paramref name="theirOverrides" /> é caminho → SHA-256 do conteúdo que
    ///     veio agora. Opcional só para não quebrar quem só quer o diff de mods;
    ///     sem ele, uma atualização que mexe apenas em configs pareceria "nada
    ///     mudou" — e pack grande publica atualização só de config o tempo todo.
    /// </summary>
    public static UpstreamMergePlan Plan(
        UpstreamSnapshot baseSnapshot,
        IReadOnlyDictionary<string, string> theirMods,
        IReadOnlyList<ModpackFile> ourFiles,
        IReadOnlyDictionary<string, string>? theirOverrides = null)
    {
        var ourMods = ourFiles
            .Where(f => f.Origin is not ModFileOrigin.Override && f.ProjectSlug is not null)
            .ToDictionary(f => f.ProjectSlug!, StringComparer.OrdinalIgnoreCase);

        var add = new List<UpstreamModChange>();
        var update = new List<UpstreamModChange>();
        var remove = new List<UpstreamModChange>();
        var conflicts = new List<UpstreamModConflict>();
        var kept = new List<string>();

        foreach (var (projectId, theirFileId) in theirMods)
        {
            var inBase = baseSnapshot.Mods.TryGetValue(projectId, out var baseFileId);
            var inOurs = ourMods.TryGetValue(projectId, out var ourFile);
            var ourFileId = ourFile?.OriginReference ?? "";
            var ourName = FileNameOf(ourFile);
            var name = NameOf(baseSnapshot, projectId);

            switch (inBase, inOurs)
            {
                // O autor adicionou um mod novo, e não temos nada com esse nome.
                case (false, false):
                    add.Add(new UpstreamModChange(projectId, name, null, theirFileId));
                    break;

                // Já existia e ninguém aqui mexeu: aplica a atualização do autor.
                case (true, true) when ourFileId == baseFileId && theirFileId != baseFileId:
                    update.Add(new UpstreamModChange(projectId, name, ourFileId, theirFileId, ourName));
                    break;

                // Os dois mexeram no mesmo mod: só o admin decide.
                case (true, true) when ourFileId != baseFileId && theirFileId != baseFileId
                                                               && ourFileId != theirFileId:
                    conflicts.Add(new UpstreamModConflict(
                        projectId, name, baseFileId, ourFileId, theirFileId,
                        UpstreamConflictKind.BothChanged, ourName));
                    break;

                // O admin apagou um mod que o autor manteve E mudou. Se o autor
                // não mexeu, a remoção do admin segue valendo em silêncio — sem
                // isto, todo mod tirado de propósito voltava à tela a cada
                // atualização, e quem recusou um mod novo uma vez o veria de
                // novo para sempre.
                case (true, false) when theirFileId != baseFileId:
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

            if (!ourMods.TryGetValue(projectId, out var ourFile))
                continue; // já não está aqui: nada a remover

            var ourFileId = ourFile.OriginReference ?? "";

            if (ourFileId == baseFileId)
                // O autor tirou o mod e o admin não tinha mexido: acompanha.
                remove.Add(new UpstreamModChange(projectId, name, ourFileId, null, FileNameOf(ourFile)));
            else
                // O admin tinha trocado a versão deste mod, e o autor o removeu.
            {
                conflicts.Add(new UpstreamModConflict(
                    projectId, name, baseFileId, ourFileId, null,
                    UpstreamConflictKind.ChangedHereRemovedThere, FileNameOf(ourFile)));
            }
        }

        // O que o admin acrescentou e nunca esteve no pack fica, sempre.
        kept.AddRange(ourMods.Keys.Where(slug =>
            !baseSnapshot.Mods.ContainsKey(slug) && !theirMods.ContainsKey(slug)));

        return new UpstreamMergePlan(add, update, remove, conflicts, kept)
        {
            Overrides = PlanOverrides(baseSnapshot, theirOverrides, ourFiles)
        };
    }

    /// <summary>
    ///     O que a atualização faz com a escolha do admin. Nula = a escolha
    ///     padrão (<see cref="UpstreamSelection.Default" />), que é o
    ///     comportamento de antes da escolha existir.
    ///     O que NÃO é marcado fica como está aqui: mod e config do admin são
    ///     copiados para o rascunho sem mudança. Como o retrato do rascunho passa
    ///     a ser o pack novo, uma mudança recusada vira "escolha sua" para a
    ///     próxima atualização — e só volta à tela se o autor mexer naquilo de
    ///     novo.
    /// </summary>
    public static UpstreamResolution Resolve(UpstreamMergePlan plan, UpstreamSelection? selection = null)
    {
        selection ??= UpstreamSelection.Default(plan);

        var ingest = new List<UpstreamModChange>();
        var dropMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var write = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dropPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in plan.Add.Where(c => selection.Includes(UpstreamItemKey.Mod(c.ProjectId))))
            ingest.Add(c);

        foreach (var c in plan.Update.Where(c => selection.Includes(UpstreamItemKey.Mod(c.ProjectId))))
        {
            // Não copia o jar velho: a ingestão traz o novo.
            dropMods.Add(c.ProjectId);
            ingest.Add(c);
        }

        foreach (var c in plan.Remove.Where(c => selection.Includes(UpstreamItemKey.Mod(c.ProjectId))))
            dropMods.Add(c.ProjectId);

        // Conflito marcado = fica o lado do autor.
        foreach (var c in plan.Conflicts.Where(c => selection.Includes(UpstreamItemKey.Mod(c.ProjectId))))
        {
            if (c.OurFileId is not null)
                dropMods.Add(c.ProjectId);

            if (c.TheirFileId is { } theirs)
                ingest.Add(new UpstreamModChange(c.ProjectId, c.Name, c.OurFileId, theirs, c.OurFileName));
        }

        var overrides = plan.Overrides;
        foreach (var path in overrides.Added.Concat(overrides.Updated)
                     .Where(p => selection.Includes(UpstreamItemKey.Override(p))))
            write.Add(path);

        foreach (var path in overrides.Removed.Where(p => selection.Includes(UpstreamItemKey.Override(p))))
            dropPaths.Add(path);

        foreach (var c in overrides.Conflicts.Where(c => selection.Includes(UpstreamItemKey.Override(c.Path))))
        {
            if (c.Kind is UpstreamConflictKind.ChangedHereRemovedThere)
                dropPaths.Add(c.Path);
            else
                write.Add(c.Path);
        }

        return new UpstreamResolution(ingest, dropMods, write, dropPaths);
    }

    /// <summary>
    ///     Mesma lógica de três vias dos mods, agora por hash de conteúdo.
    ///     Devolve caminhos, não contagens: o caso de uso precisa saber QUAIS
    ///     configs sair do rascunho, não quantos.
    /// </summary>
    private static UpstreamOverridePlan PlanOverrides(
        UpstreamSnapshot baseSnapshot,
        IReadOnlyDictionary<string, string>? theirOverrides,
        IReadOnlyList<ModpackFile> ourFiles)
    {
        if (theirOverrides is null)
            return UpstreamOverridePlan.Empty;

        var ours = ourFiles
            .Where(f => f.Origin is ModFileOrigin.Override)
            .ToDictionary(f => f.Path, f => f.Sha256, StringComparer.OrdinalIgnoreCase);

        var added = new List<string>();
        var updated = new List<string>();
        var removed = new List<string>();
        var conflicts = new List<UpstreamOverrideConflict>();

        foreach (var (path, theirSha) in theirOverrides)
        {
            var baseSha = baseSnapshot.Overrides.GetValueOrDefault(path);

            if (!ours.TryGetValue(path, out var ourSha))
            {
                if (baseSha is null)
                    added.Add(path); // config novo do autor
                else if (baseSha != theirSha)
                {
                    // O admin apagou e o autor mudou: mesma regra dos mods. Antes
                    // isto voltava como "config novo" e desfazia a remoção.
                    conflicts.Add(new UpstreamOverrideConflict(
                        path, UpstreamConflictKind.RemovedHereKeptThere, null));
                }

                continue;
            }

            if (ourSha == theirSha)
                continue; // idêntico: nada a fazer

            // Intocado desde a importação: pode receber a versão do autor.
            // Diferente da base: o admin editou — conflito, e o dele fica até
            // ele marcar o contrário. Se o autor nem mexeu (theirs == base), não
            // há o que decidir.
            if (baseSha is not null && ourSha == baseSha)
                updated.Add(path);
            else if (baseSha != theirSha)
                conflicts.Add(new UpstreamOverrideConflict(path, UpstreamConflictKind.BothChanged, ourSha));
        }

        // O autor tirou o config do pack. Mesma regra dos mods: se o admin não
        // tinha mexido, acompanha a remoção; se tinha, o dele fica — remover a
        // customização dele seria a única coisa pior que ignorá-la.
        foreach (var (path, baseSha) in baseSnapshot.Overrides)
        {
            if (theirOverrides.ContainsKey(path) || !ours.TryGetValue(path, out var ourSha))
                continue;

            if (ourSha == baseSha)
                removed.Add(path);
            else
                conflicts.Add(new UpstreamOverrideConflict(
                    path, UpstreamConflictKind.ChangedHereRemovedThere, ourSha));
        }

        return new UpstreamOverridePlan(added, updated, removed, conflicts);
    }

    private static string NameOf(UpstreamSnapshot snapshot, string projectId) =>
        snapshot.Names.GetValueOrDefault(projectId, projectId);

    private static string? FileNameOf(ModpackFile? file) =>
        file is null ? null : Path.GetFileName(file.Path);
}

/// <summary>
///     O que a atualização proporia. <see cref="Conflicts" /> nunca é aplicado
///     sozinho — na dúvida, o lado do admin prevalece e a decisão fica com ele.
/// </summary>
public sealed record UpstreamMergePlan(
    IReadOnlyList<UpstreamModChange> Add,
    IReadOnlyList<UpstreamModChange> Update,
    IReadOnlyList<UpstreamModChange> Remove,
    IReadOnlyList<UpstreamModConflict> Conflicts,
    IReadOnlyList<string> Kept)
{
    /// <summary>O que muda em configs e scripts.</summary>
    public UpstreamOverridePlan Overrides { get; init; } = UpstreamOverridePlan.Empty;

    public int TotalChanges =>
        Add.Count + Update.Count + Remove.Count
        + Overrides.Added.Count + Overrides.Updated.Count + Overrides.Removed.Count;

    public int TotalConflicts => Conflicts.Count + Overrides.Conflicts.Count;

    public bool IsEmpty => TotalChanges is 0 && TotalConflicts is 0;
}

/// <summary>
///     Configs e scripts: o que entra, o que muda, o que sai e onde o autor e o
///     admin mexeram no mesmo arquivo.
/// </summary>
public sealed record UpstreamOverridePlan(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> Removed,
    IReadOnlyList<UpstreamOverrideConflict> Conflicts)
{
    public static UpstreamOverridePlan Empty { get; } = new([], [], [], []);

    public int TotalChanges => Added.Count + Updated.Count + Removed.Count;
}

/// <param name="OurFileName">Nome do jar que está aqui hoje, para a tela; nulo se não há.</param>
public sealed record UpstreamModChange(
    string ProjectId, string Name, string? FromFileId, string? ToFileId, string? OurFileName = null);

public sealed record UpstreamModConflict(
    string ProjectId,
    string Name,
    string? BaseFileId,
    string? OurFileId,
    string? TheirFileId,
    UpstreamConflictKind Kind,
    string? OurFileName = null);

/// <param name="OurSha256">Conteúdo que está aqui hoje (nulo se o admin apagou), para mostrar o lado dele.</param>
public sealed record UpstreamOverrideConflict(string Path, UpstreamConflictKind Kind, string? OurSha256);

public enum UpstreamConflictKind
{
    /// <summary>Autor e admin trocaram a versão do mesmo mod (ou editaram o mesmo config).</summary>
    BothChanged,

    /// <summary>O admin removeu daqui; o autor continua incluindo (e mudou).</summary>
    RemovedHereKeptThere,

    /// <summary>O admin trocou a versão; o autor removeu do pack.</summary>
    ChangedHereRemovedThere
}

/// <summary>
///     Chave estável de um item do plano, a mesma na simulação e na aplicação.
///     Prefixo porque mod e config vivem em espaços de nome diferentes: um
///     projeto chamado "config/a.toml" é absurdo, mas não impossível.
/// </summary>
public static class UpstreamItemKey
{
    public static string Mod(string projectId) => $"mod:{projectId}";

    public static string Override(string path) => $"cfg:{path}";
}

/// <summary>
///     O que o admin marcou. Lista do que ENTRA, não do que sai: se o autor
///     publicar outra coisa entre a simulação e o "Criar rascunho", o que o
///     admin não viu não entra.
/// </summary>
public sealed record UpstreamSelection(IReadOnlySet<string> Accepted)
{
    public bool Includes(string key) => Accepted.Contains(key);

    /// <summary>Marcado o que não conflita; conflitos desmarcados (fica o lado do admin).</summary>
    public static UpstreamSelection Default(UpstreamMergePlan plan)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in plan.Add.Concat(plan.Update).Concat(plan.Remove))
            keys.Add(UpstreamItemKey.Mod(c.ProjectId));

        foreach (var path in plan.Overrides.Added.Concat(plan.Overrides.Updated).Concat(plan.Overrides.Removed))
            keys.Add(UpstreamItemKey.Override(path));

        return new UpstreamSelection(keys);
    }
}

/// <summary>
///     O plano depois da escolha: mods a baixar, mods que não são copiados da
///     versão atual, configs que recebem o conteúdo do autor e configs que saem.
/// </summary>
public sealed record UpstreamResolution(
    IReadOnlyList<UpstreamModChange> ModsToIngest,
    IReadOnlySet<string> ModsToDrop,
    IReadOnlySet<string> OverridesToWrite,
    IReadOnlySet<string> OverridesToDrop)
{
    public bool IsEmpty =>
        ModsToIngest.Count is 0 && ModsToDrop.Count is 0
                                && OverridesToWrite.Count is 0 && OverridesToDrop.Count is 0;
}
