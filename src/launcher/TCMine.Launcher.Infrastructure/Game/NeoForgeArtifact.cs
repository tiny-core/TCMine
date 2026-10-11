namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Onde está o instalador de uma build do NeoForge, como chamá-lo e que
///     versão ele deixa em <c>versions/</c>.
///     Existe porque o NeoForge tem DOIS formatos, e o launcher só conhecia o
///     novo. No Minecraft 1.20.1 ele ainda era um fork do Forge e manteve o
///     artefato, a numeração e o nome de versão dele; da 1.20.2 em diante passou
///     para o artefato próprio. O painel oferece os dois (ver
///     <c>NeoForgeVersionSource</c> no servidor), então um pack 1.20.1 chegava
///     aqui e morria num 404 do maven.
///     Classe pura, separada do <see cref="NeoForgeInstaller" />, para a regra
///     ser testável sem rede e sem Java.
/// </summary>
public sealed record NeoForgeArtifact(Uri InstallerUrl, string InstallFlag, string VersionName, bool IsLegacy)
{
    private const string LegacyMinecraft = "1.20.1";

    private const string Maven = "https://maven.neoforged.net/releases/net/neoforged";

    public static NeoForgeArtifact For(string minecraftVersion, string neoForgeVersion)
    {
        var minecraft = minecraftVersion.Trim();
        var loader = neoForgeVersion.Trim();

        if (minecraft != LegacyMinecraft)
        {
            return new NeoForgeArtifact(
                new Uri($"{Maven}/neoforge/{loader}/neoforge-{loader}-installer.jar"),
                "--install-client",
                $"neoforge-{loader}",
                false);
        }

        // O painel grava só a parte do NeoForge ("47.1.106"), mas um pack
        // importado pode trazer a forma completa do maven ("1.20.1-47.1.106").
        // Aceitar as duas evita montar "1.20.1-1.20.1-47.1.106".
        var prefix = $"{LegacyMinecraft}-";

        if (loader.StartsWith(prefix, StringComparison.Ordinal))
            loader = loader[prefix.Length..];

        var coordinate = $"{LegacyMinecraft}-{loader}";

        return new NeoForgeArtifact(
            new Uri($"{Maven}/forge/{coordinate}/forge-{coordinate}-installer.jar"),

            // A forma com hífen só entrou no instalador depois; a camelCase
            // existe desde o instalador do Forge, de onde este descende.
            "--installClient",
            $"{LegacyMinecraft}-forge-{loader}",
            true);
    }

    /// <summary>
    ///     O nome da versão já instalada, ou nulo.
    ///     O sinal é o JSON da versão existir — o mesmo que o CmlLib procura
    ///     para abrir.
    ///     No formato antigo o nome esperado é o do Forge, mas quem o escolhe é
    ///     o instalador, não nós. Por isso, se o esperado não estiver lá, vale
    ///     qualquer versão do 1.20.1 que termine nesta build e tenha "forge" no
    ///     nome: errar o palpite do nome não pode virar "não instalou".
    ///     Limite conhecido: o Forge do 1.20.1 usa o mesmo esquema de nome. Uma
    ///     build com o MESMO número nos dois loaders, na mesma máquina, seria
    ///     confundida — as numerações se separaram logo após o fork, então é
    ///     teórico, mas fica registrado.
    /// </summary>
    public string? FindInstalled(string versionsDirectory)
    {
        if (HasProfile(versionsDirectory, VersionName))
            return VersionName;

        if (!IsLegacy || !Directory.Exists(versionsDirectory))
            return null;

        var build = VersionName[(VersionName.LastIndexOf("-forge-", StringComparison.Ordinal) + "-forge".Length)..];

        foreach (var directory in Directory.EnumerateDirectories(versionsDirectory))
        {
            var name = Path.GetFileName(directory);

            if (name.StartsWith($"{LegacyMinecraft}-", StringComparison.Ordinal)
                && name.EndsWith(build, StringComparison.Ordinal)
                && name.Contains("forge", StringComparison.OrdinalIgnoreCase)
                && HasProfile(versionsDirectory, name))
                return name;
        }

        return null;
    }

    private static bool HasProfile(string versionsDirectory, string name) =>
        File.Exists(Path.Combine(versionsDirectory, name, $"{name}.json"));
}
