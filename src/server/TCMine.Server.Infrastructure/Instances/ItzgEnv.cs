using System.Security.Cryptography;
using System.Text;
using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Traduz uma versão fixada nas variáveis de ambiente do itzg/minecraft-server.
///     O container só entende strings; aqui é onde o nosso domínio vira config dele.
/// </summary>
public static class ItzgEnv
{
    // O itzg usa nomes próprios de "TYPE" por loader.
    public static string ToServerType(ModLoader loader)
    {
        return loader switch
        {
            ModLoader.Vanilla => "VANILLA",
            ModLoader.Forge => "FORGE",
            ModLoader.NeoForge => "NEOFORGE",
            ModLoader.Fabric => "FABRIC",
            ModLoader.Quilt => "QUILT",
            _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null)
        };
    }

    /// <summary>
    ///     Nome da variável que fixa a build do loader. NÃO é "{TYPE}_VERSION"
    ///     para todos: no Fabric e no Quilt o itzg lê FABRIC_LOADER_VERSION e
    ///     QUILT_LOADER_VERSION. Montar o nome por concatenação gerava
    ///     FABRIC_VERSION, que o itzg ignora em silêncio e troca pelo loader
    ///     mais recente — o servidor subia com uma build que o pack nunca testou.
    ///     Vanilla não tem loader: nulo.
    /// </summary>
    public static string? LoaderVersionVariable(ModLoader loader) => loader switch
    {
        ModLoader.Forge => "FORGE_VERSION",
        ModLoader.NeoForge => "NEOFORGE_VERSION",
        ModLoader.Fabric => "FABRIC_LOADER_VERSION",
        ModLoader.Quilt => "QUILT_LOADER_VERSION",
        _ => null
    };

    /// <summary>
    ///     As variáveis que decidem QUE jogo o container roda: tipo, versão do
    ///     Minecraft e build do loader — tudo vindo do modpack e da versão fixada.
    ///     A versão do Minecraft em branco é recusada aqui, e não repassada: o
    ///     itzg trata VERSION ausente como LATEST, e o servidor subiria na última
    ///     versão do jogo com mods de outra — crash no arranque, com um log que
    ///     não aponta para a causa.
    /// </summary>
    public static IReadOnlyList<string> GameVariables(
        string minecraftVersion, ModLoader loader, string? loaderVersion)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            throw new InvalidOperationException(
                "O modpack não tem versão do Minecraft definida; sem ela o container subiria na versão mais recente.");
        }

        var env = new List<string>
        {
            $"TYPE={ToServerType(loader)}",
            $"VERSION={minecraftVersion.Trim()}"
        };

        // Build do loader em branco = deixa o itzg escolher a recomendada para
        // aquele Minecraft. Mandar "NEOFORGE_VERSION=" vazio não é o mesmo que
        // omitir, e cada tipo do itzg reage de um jeito.
        if (LoaderVersionVariable(loader) is { } variable && !string.IsNullOrWhiteSpace(loaderVersion))
            env.Add($"{variable}={loaderVersion.Trim()}");

        return env;
    }

    /// <summary>
    ///     Impressão digital da spec do container. Vai num label, e é por ela que
    ///     o orquestrador sabe se o container existente ainda corresponde ao que o
    ///     servidor pede: o Docker não deixa mudar o ambiente de um container
    ///     criado, então versão, loader, memória ou porta novos exigem recriá-lo.
    ///     Ordenado para a ordem da lista não mudar o resultado.
    /// </summary>
    public static string Fingerprint(string image, IEnumerable<string> env, IEnumerable<string> extra)
    {
        var canonical = string.Join('\n', new[] { image }.Concat(env.Order(StringComparer.Ordinal))
            .Concat(extra.Order(StringComparer.Ordinal)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
