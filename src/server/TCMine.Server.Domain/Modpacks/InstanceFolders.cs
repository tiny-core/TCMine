namespace TCMine.Server.Domain.Modpacks;

/// <summary>
///     As pastas de uma instância que não são <c>mods/</c>.
///     Existem porque um modpack não traz só mods: o All the Mods 10 lista 481
///     mods e 4 shaderpacks no mesmo manifesto, e um <c>.zip</c> de shader
///     gravado em <c>mods/</c> derruba o jogo no arranque.
///     A lista vive aqui, e não repetida no resolver e nas consultas, porque as
///     duas pontas têm de concordar sobre o que é o quê — quem decide a pasta ao
///     ingerir e quem separa as abas ao listar.
/// </summary>
public static class InstanceFolders
{
    public const string Mods = "mods";
    public const string Shaderpacks = "shaderpacks";
    public const string Resourcepacks = "resourcepacks";
    public const string Datapacks = "datapacks";

    /// <summary>
    ///     O que a aba de recursos mostra. Tudo o que não está aqui e não é
    ///     override é mod.
    /// </summary>
    public static readonly string[] Assets = [Shaderpacks, Resourcepacks, Datapacks];

    /// <summary>Prefixo com barra, como aparece no <c>Path</c> do arquivo.</summary>
    public static string Prefix(string folder) => $"{folder}/";

    /// <summary>Rótulo legível para a interface.</summary>
    public static string Label(string folder) => folder switch
    {
        Shaderpacks => "shaderpack",
        Resourcepacks => "resource pack",
        Datapacks => "data pack",
        _ => "mod"
    };

    /// <summary>A pasta de um caminho, ou <c>mods</c> quando não reconhecida.</summary>
    public static string Of(string path)
    {
        var barra = path.IndexOf('/');
        if (barra <= 0)
            return Mods;

        var pasta = path[..barra];
        return Assets.Contains(pasta, StringComparer.OrdinalIgnoreCase) ? pasta.ToLowerInvariant() : Mods;
    }
}
