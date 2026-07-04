namespace TCMine_Domain.Launcher;

/// <summary>
/// Define os ficheiros/pastas da pasta do jogo que pertencem ao <b>jogador</b> (não ao modpack):
/// keybinds/opções, shaders selecionados, dados/cache de minimapa. Usado tanto para preservar essas
/// configs quando os overrides do modpack são reaplicados num update, como para o sync com o servidor
/// ([[concepts/player-config-sync]]). Caminhos relativos a <c>minecraft/</c>.
///
/// <b>Só mundos de servidor (multiplayer):</b> o cache de mapa incluído é o dos mundos do servidor
/// (subpastas <c>mp</c>/<c>Multiplayer*</c>), <b>não</b> o dos mundos singleplayer locais que o jogador
/// cria (<c>sp</c>/<c>Singleplayer*</c>) — estes ficam de fora de propósito.
/// </summary>
public static class PlayerDataProfile
{
    public static readonly IReadOnlyList<string> Patterns =
    [
        "options.txt",
        "optionsshaders.txt",
        "shaderpacks/*.txt",
        "config/xaero*",
        // Xaero: waypoints + cache do mapa-mundo, só dos servidores (não Singleplayer_*)
        "XaeroWaypoints/Multiplayer*",
        "XaeroWorldMap/Multiplayer*",
        // JourneyMap: configs globais + dados/cache dos servidores (data/mp), nunca os locais (data/sp)
        "journeymap/config",
        "journeymap/data/mp"
    ];

    /// <summary>Caminhos relativos ('/') de todos os ficheiros player-owned que existem em gameDir.</summary>
    public static IReadOnlyList<string> EnumerateExisting(string gameDir)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) return result;

        foreach (var pattern in Patterns)
            if (pattern.Contains('*'))
                AddGlob(gameDir, pattern, result);
            else
                AddPath(gameDir, pattern, result);

        return result;
    }

    private static void AddPath(string gameDir, string rel, List<string> result)
    {
        var full = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) result.Add(rel);
        else if (Directory.Exists(full)) AddDirectory(gameDir, full, result);
    }

    private static void AddGlob(string gameDir, string pattern, List<string> result)
    {
        var slash = pattern.LastIndexOf('/');
        var dirRel = slash >= 0 ? pattern[..slash] : string.Empty;
        var glob = slash >= 0 ? pattern[(slash + 1)..] : pattern;
        var dirFull = Path.Combine(gameDir, dirRel.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dirFull)) return;

        foreach (var entry in Directory.EnumerateFileSystemEntries(dirFull, glob))
            if (File.Exists(entry)) result.Add(ToRel(gameDir, entry));
            else if (Directory.Exists(entry)) AddDirectory(gameDir, entry, result);
    }

    private static void AddDirectory(string gameDir, string dirFull, List<string> result)
    {
        foreach (var file in Directory.EnumerateFiles(dirFull, "*", SearchOption.AllDirectories))
            result.Add(ToRel(gameDir, file));
    }

    private static string ToRel(string gameDir, string full) =>
        Path.GetRelativePath(gameDir, full).Replace(Path.DirectorySeparatorChar, '/');
}
