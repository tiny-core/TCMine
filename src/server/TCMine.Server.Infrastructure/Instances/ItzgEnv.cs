using System.Globalization;
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
    private const string Repository = "itzg/minecraft-server";

    /// <summary>
    ///     A imagem do itzg com o Java que aquela versão do Minecraft pede.
    ///     Antes era sempre <c>:latest</c>, que acompanha o Java do Minecraft mais
    ///     NOVO (hoje o 25). Um pack de 1.16.5 subia num Java que o Forge dele não
    ///     aguenta, e morria no arranque com um erro de classloader que não fala
    ///     em Java.
    /// </summary>
    public static string Image(string minecraftVersion) => $"{Repository}:{JavaTag(minecraftVersion)}";

    /// <summary>
    ///     A tag de Java para uma versão do Minecraft, pelos requisitos da Mojang:
    ///     Java 8 até a 1.16.5, 16 na 1.17, 17 da 1.18 à 1.20.4, 21 da 1.20.5 em
    ///     diante e 25 na série 26.
    ///     Tabela fixa, e não consulta à Mojang: iniciar um servidor não pode
    ///     depender de uma API externa estar no ar, e as faixas antigas não mudam.
    ///     O que a tabela não conhece — uma série mais nova que a 26, um snapshot
    ///     — vai para <c>latest</c>, que é a tag que o itzg mantém no Java da
    ///     versão mais recente do jogo.
    /// </summary>
    public static string JavaTag(string minecraftVersion)
    {
        if (!TryParseRelease(minecraftVersion, out var version))
            return "latest";

        return version switch
        {
            // Numeração por ano (26.1, 26.2…). Acima da 26 ainda não se sabe.
            (26, _, _) => "java25",
            ( > 26, _, _) => "latest",

            (1, <= 16, _) => "java8",
            (1, 17, _) => "java16",
            (1, 18 or 19, _) => "java17",
            (1, 20, <= 4) => "java17",
            (1, _, _) => "java21",

            _ => "latest"
        };
    }

    /// <summary>
    ///     "1.20.5" → (1, 20, 5); "1.21" → (1, 21, 0). Um sufixo de pré-lançamento
    ///     ("1.20.5-rc1") é ignorado: pede o mesmo Java da versão final. O que não
    ///     tem essa forma (snapshots como "24w14a") não é reconhecido.
    /// </summary>
    private static bool TryParseRelease(string? text, out (int Major, int Minor, int Patch) version)
    {
        version = default;

        var core = (text ?? "").Trim().Split('-', ' ')[0];
        var parts = core.Split('.');

        if (parts.Length is < 2 or > 3)
            return false;

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }

        version = (numbers[0], numbers[1], numbers[2]);
        return true;
    }

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

        var env = new List<string> { $"TYPE={ToServerType(loader)}", $"VERSION={minecraftVersion.Trim()}" };

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
