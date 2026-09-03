namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     Regras de disco da instância, isoladas para poderem ser testadas.
/// </summary>
public static class InstanceLayout
{
    /// <summary>
    ///     Este arquivo pode ser um hardlink para o content store?
    ///     Só os jars: eles são lidos e nunca reescritos. Um config com hardlink
    ///     seria corrompido na primeira vez que o jogo o reescrevesse — e como o
    ///     blob é COMPARTILHADO entre todas as instâncias, a corrupção viajaria
    ///     para todo modpack que usasse o mesmo arquivo.
    ///     O custo de copiar o resto é baixo: configs são quilobytes, e os
    ///     megabytes estão em mods/.
    /// </summary>
    public static bool CanHardLink(string relativePath) =>
        relativePath.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase);
}
