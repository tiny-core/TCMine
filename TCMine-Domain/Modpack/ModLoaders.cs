namespace TCMine_Domain.Modpack;

/// <summary>
///     Carregador de mods de um modpack/servidor. O projeto não fica preso ao NeoForge —
///     outros loaders são suportados. Compartilhado por launcher e servidor (Core).
/// </summary>
public enum ModLoader
{
    NeoForge,
    Forge,
    Fabric,
    Quilt
}

/// <summary>Utilitários de loader — interpretação do id do manifesto e nome de exibição.</summary>
public static class ModLoaders
{
    /// <summary>
    ///     Interpreta o id de loader do manifesto CurseForge (ex.: <c>"neoforge-21.1.77"</c>,
    ///     <c>"fabric-0.15.11"</c>) em (loader, versão). Prefixo desconhecido → NeoForge como padrão.
    /// </summary>
    public static (ModLoader Loader, string Version) ParseId(string? loaderId)
    {
        if (string.IsNullOrWhiteSpace(loaderId)) return (ModLoader.NeoForge, string.Empty);

        // O id vem como "<nome>-<versão>"; separa no primeiro hífen
        var dash = loaderId.IndexOf('-');
        var name = dash > 0 ? loaderId[..dash] : loaderId;
        var version = dash > 0 ? loaderId[(dash + 1)..] : string.Empty;

        var loader = name.Trim().ToLowerInvariant() switch
        {
            "neoforge" => ModLoader.NeoForge,
            "forge" => ModLoader.Forge,
            "fabric" => ModLoader.Fabric,
            "quilt" => ModLoader.Quilt,
            _ => ModLoader.NeoForge
        };

        return (loader, version);
    }

    /// <summary>Nome amigável para a UI.</summary>
    public static string DisplayName(ModLoader loader)
    {
        return loader switch
        {
            ModLoader.NeoForge => "NeoForge",
            ModLoader.Forge => "Forge",
            ModLoader.Fabric => "Fabric",
            ModLoader.Quilt => "Quilt",
            _ => loader.ToString()
        };
    }
}