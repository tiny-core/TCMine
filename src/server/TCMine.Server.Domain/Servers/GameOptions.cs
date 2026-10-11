using System.Globalization;
using System.Text.Json;

namespace TCMine.Server.Domain.Servers;

/// <summary>Que campo uma opção ganha no formulário, e como o valor dela é conferido.</summary>
public enum GameOptionKind
{
    /// <summary>Ligado ou desligado ("true" / "false").</summary>
    Toggle,

    /// <summary>Um valor de uma lista fechada.</summary>
    Choice,

    /// <summary>Um inteiro dentro de uma faixa.</summary>
    Number,

    /// <summary>Texto curto.</summary>
    Text
}

/// <summary>Um valor possível de uma opção de lista, e como ele aparece na tela.</summary>
public sealed record GameOptionChoice(string Value, string Label);

/// <summary>
///     Uma opção do servidor de jogo que o admin pode definir.
///     <see cref="Key" /> é o nome da variável de ambiente da imagem
///     itzg/minecraft-server — é assim que a opção chega ao jogo.
/// </summary>
public sealed record GameOption(
    string Key,
    string Group,
    string Label,
    string Description,
    GameOptionKind Kind)
{
    public IReadOnlyList<GameOptionChoice> Choices { get; init; } = [];
    public int Min { get; init; }
    public int Max { get; init; } = int.MaxValue;
    public int MaxLength { get; init; } = 64;

    /// <summary>Conferência extra do texto. Nulo = qualquer texto sem caracteres de controle.</summary>
    public Func<string, bool>? IsAcceptable { get; init; }
}

/// <summary>
///     O catálogo das opções de jogo e as regras dos valores.
///     Catálogo FECHADO, de propósito. A imagem do itzg tem mais de cem variáveis,
///     e várias são do próprio TCMine — tipo e versão do jogo, memória, RCON,
///     whitelist. Um campo livre deixaria o admin trocar a versão do Minecraft
///     por baixo do modpack ou desligar o RCON de que o painel depende. Aqui só
///     entra o que é seguro mexer, com o tipo certo e a explicação do efeito.
///     Fica de fora, por decisão: ONLINE_MODE (desligá-lo aceita contas piratas e
///     torna a whitelist falsificável) e a pausa automática do itzg (para o
///     processo enquanto o painel consulta status e jogadores).
///     Opção não definida = variável não enviada: vale o que o modpack traz no
///     server.properties dele.
/// </summary>
public static class GameOptions
{
    public const string On = "true";
    public const string Off = "false";

    private const string Game = "Jogo";
    private const string World = "Mundo";
    private const string Performance = "Desempenho";
    private const string Players = "Jogadores";
    private const string Server = "Sistema";

    public static IReadOnlyList<GameOption> Catalog { get; } =
    [
        // ---------- Jogo ----------
        new("DIFFICULTY", Game, "Dificuldade",
            "Quanto dano os monstros causam e se a fome mata. Em pacífico não nascem monstros hostis.",
            GameOptionKind.Choice)
        {
            Choices =
            [
                new("peaceful", "Pacífico"), new("easy", "Fácil"), new("normal", "Normal"), new("hard", "Difícil")
            ]
        },
        new("MODE", Game, "Modo de jogo",
            "O modo em que um jogador novo entra. Quem já jogou mantém o seu, a não ser com \"Forçar modo de jogo\".",
            GameOptionKind.Choice)
        {
            Choices =
            [
                new("survival", "Sobrevivência"), new("creative", "Criativo"), new("adventure", "Aventura"),
                new("spectator", "Espectador")
            ]
        },
        new("FORCE_GAMEMODE", Game, "Forçar modo de jogo",
            "Devolve todo jogador ao modo de jogo do servidor a cada entrada.", GameOptionKind.Toggle),
        new("HARDCORE", Game, "Hardcore",
            "Morreu, vira espectador para sempre naquele mundo. Só faz efeito num mundo novo.",
            GameOptionKind.Toggle),
        new("PVP", Game, "PvP", "Jogadores podem ferir uns aos outros.", GameOptionKind.Toggle),
        new("ALLOW_FLIGHT", Game, "Permitir voo",
            "Não expulsa quem voa em sobrevivência. Ligue se o pack tem mochilas a jato, asas ou máquinas voadoras — "
            + "desligado, o servidor trata esses jogadores como trapaceiros.",
            GameOptionKind.Toggle),
        new("ENABLE_COMMAND_BLOCK", Game, "Blocos de comando",
            "Blocos de comando funcionam. Mapas de aventura e algumas missões de modpack dependem deles.",
            GameOptionKind.Toggle),
        new("ALLOW_NETHER", Game, "Nether", "Portais para o Nether funcionam.", GameOptionKind.Toggle),
        new("SPAWN_MONSTERS", Game, "Monstros", "Monstros hostis nascem no mundo.", GameOptionKind.Toggle),
        new("SPAWN_ANIMALS", Game, "Animais", "Animais nascem no mundo.", GameOptionKind.Toggle),
        new("SPAWN_NPCS", Game, "Aldeões", "Aldeões nascem nas vilas.", GameOptionKind.Toggle),

        // ---------- Mundo ----------
        new("SEED", World, "Semente",
            "Define o mundo que será gerado. Só vale ao criar o mundo; depois, mudar não altera o que já existe.",
            GameOptionKind.Text) { MaxLength = 64 },
        new("LEVEL_TYPE", World, "Tipo de mundo",
            "Gerador do mundo, como minecraft:flat ou o de um mod do pack. Só vale ao criar o mundo.",
            GameOptionKind.Text) { MaxLength = 64, IsAcceptable = IsToken },
        new("GENERATE_STRUCTURES", World, "Gerar estruturas",
            "Vilas, templos e fortalezas aparecem em terreno novo. Só afeta áreas ainda não exploradas.",
            GameOptionKind.Toggle),
        new("SPAWN_PROTECTION", World, "Proteção do spawn",
            "Raio, em blocos, em volta do ponto de nascimento onde só operadores constroem. Zero desliga.",
            GameOptionKind.Number) { Min = 0, Max = 256 },
        new("MAX_WORLD_SIZE", World, "Tamanho máximo do mundo",
            "Raio da borda do mundo, em blocos. Limita o quanto o mundo cresce em disco.",
            GameOptionKind.Number) { Min = 1, Max = 29_999_984 },

        // ---------- Desempenho ----------
        new("VIEW_DISTANCE", Performance, "Distância de visão",
            "Quantos chunks o servidor envia em volta de cada jogador. É o ajuste que mais pesa em CPU, memória e "
            + "rede: baixar de 10 para 8 alivia bastante um servidor lento.",
            GameOptionKind.Number) { Min = 2, Max = 32 },
        new("SIMULATION_DISTANCE", Performance, "Distância de simulação",
            "Até onde máquinas, plantações e criaturas continuam a funcionar em volta do jogador. Menor = menos CPU, "
            + "mas fazendas longe do jogador param.",
            GameOptionKind.Number) { Min = 2, Max = 32 },
        new("USE_AIKAR_FLAGS", Performance, "Ajustes de memória do Aikar",
            "Parâmetros de coleta de lixo do Java afinados para Minecraft: menos travadas com muita memória em uso. "
            + "Recomendado para modpacks grandes.",
            GameOptionKind.Toggle),
        new("USE_MEOWICE_FLAGS", Performance, "Ajustes de memória do MeowIce",
            "Variante dos ajustes do Aikar com otimizações do Java 17 em diante. Use um ou outro, não os dois.",
            GameOptionKind.Toggle),
        new("MAX_TICK_TIME", Performance, "Tempo máximo de um tick",
            "Milissegundos até o servidor se considerar travado e se desligar. Modpacks pesados estouram o padrão "
            + "(60000) ao gerar terreno; -1 desliga essa vigilância.",
            GameOptionKind.Number) { Min = -1, Max = 3_600_000 },
        new("SYNC_CHUNK_WRITES", Performance, "Gravação síncrona de chunks",
            "Desligado, o mundo grava em disco sem esperar confirmação: mais rápido em disco lento, com risco de "
            + "perder os últimos segundos numa queda de energia.",
            GameOptionKind.Toggle),
        new("NETWORK_COMPRESSION_THRESHOLD", Performance, "Limite de compressão de rede",
            "Tamanho, em bytes, a partir do qual os pacotes são comprimidos. Maior = menos CPU e mais banda; "
            + "-1 desliga a compressão.",
            GameOptionKind.Number) { Min = -1, Max = 65_535 },
        new("ENTITY_BROADCAST_RANGE_PERCENTAGE", Performance, "Alcance de entidades (%)",
            "A que distância criaturas e itens são enviados aos jogadores, em relação ao padrão. Abaixo de 100 "
            + "alivia a rede em servidores cheios; as criaturas surgem mais perto.",
            GameOptionKind.Number) { Min = 10, Max = 1000 },
        new("PAUSE_WHEN_EMPTY_SECONDS", Performance, "Pausar o mundo sem jogadores",
            "Segundos sem ninguém online até o mundo parar de avançar, poupando CPU. Zero ou negativo nunca pausa. "
            + "Só existe do Minecraft 1.21.2 em diante.",
            GameOptionKind.Number) { Min = -1, Max = 86_400 },

        // ---------- Jogadores ----------
        new("OPS", Players, "Operadores",
            "Nomes das contas com poder de administrador dentro do jogo, separados por vírgula.",
            GameOptionKind.Text) { MaxLength = 400, IsAcceptable = IsNameList },
        new("PLAYER_IDLE_TIMEOUT", Players, "Expulsar inativos",
            "Minutos parado até o jogador ser desconectado. Zero nunca expulsa.",
            GameOptionKind.Number) { Min = 0, Max = 1440 },
        new("ENFORCE_SECURE_PROFILE", Players, "Exigir chat assinado",
            "Só entra quem tem a chave de chat da Mojang. Muitos modpacks desligam para os mods de chat funcionarem.",
            GameOptionKind.Toggle),

        // ---------- Sistema ----------
        new("MOTD", Server, "Mensagem do servidor",
            "O texto que aparece sob o nome do servidor na lista de multijogador.",
            GameOptionKind.Text) { MaxLength = 120 },
        new("TZ", Server, "Fuso horário",
            "Fuso das horas nos logs do servidor, como Europe/Lisbon ou America/Sao_Paulo. O padrão é UTC.",
            GameOptionKind.Text) { MaxLength = 64, IsAcceptable = IsToken },
        new("STOP_SERVER_ANNOUNCE_DELAY", Server, "Aviso antes de parar",
            "Segundos entre avisar os jogadores e desligar de fato, para dar tempo de se abrigarem.",
            GameOptionKind.Number) { Min = 0, Max = 600 }
    ];

    private static readonly Dictionary<string, GameOption> ByKey =
        Catalog.ToDictionary(o => o.Key, StringComparer.Ordinal);

    /// <summary>
    ///     Confere e limpa o que veio do formulário. Valor em branco = opção não
    ///     definida, e sai do resultado. Devolve o erro em texto, ou nulo.
    /// </summary>
    public static (IReadOnlyDictionary<string, string> Options, string? Error) Normalize(
        IReadOnlyDictionary<string, string>? input)
    {
        var clean = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, raw) in input ?? new Dictionary<string, string>())
        {
            var value = raw?.Trim() ?? "";
            if (value.Length == 0)
                continue;

            // Chave fora do catálogo nunca chega ao container: é a garantia de
            // que nenhuma borda consegue injetar uma variável que o TCMine
            // controla.
            if (!ByKey.TryGetValue(key, out var option))
                return (clean, $"Opção desconhecida: {key}.");

            if (Check(option, value) is { } error)
                return (clean, error);

            clean[key] = option.Kind is GameOptionKind.Toggle ? value.ToLowerInvariant() : value;
        }

        if (clean.GetValueOrDefault("USE_AIKAR_FLAGS") == On && clean.GetValueOrDefault("USE_MEOWICE_FLAGS") == On)
            return (clean, "Escolha só um dos ajustes de memória: Aikar ou MeowIce.");

        return (clean, null);
    }

    private static string? Check(GameOption option, string value)
    {
        switch (option.Kind)
        {
            case GameOptionKind.Toggle:
                return value.Equals(On, StringComparison.OrdinalIgnoreCase)
                       || value.Equals(Off, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : $"{option.Label}: valor inválido.";

            case GameOptionKind.Choice:
                return option.Choices.Any(c => c.Value == value) ? null : $"{option.Label}: valor inválido.";

            case GameOptionKind.Number:
                return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
                       && n >= option.Min
                       && n <= option.Max
                    ? null
                    : $"{option.Label}: informe um número entre {option.Min} e {option.Max}.";

            default:
                if (value.Length > option.MaxLength)
                    return $"{option.Label}: no máximo {option.MaxLength} caracteres.";

                // Caracteres de controle nunca: uma quebra de linha aqui iria
                // parar dentro do server.properties como uma segunda propriedade.
                if (value.Any(char.IsControl) || option.IsAcceptable?.Invoke(value) == false)
                    return $"{option.Label}: há caracteres que não são aceitos.";

                return null;
        }
    }

    /// <summary>O que é guardado no banco. Sem opções definidas, nulo.</summary>
    public static string? Serialize(IReadOnlyDictionary<string, string> options) =>
        options.Count == 0
            ? null
            : JsonSerializer.Serialize(new SortedDictionary<string, string>(
                options.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal));

    /// <summary>
    ///     As opções guardadas. Texto ilegível ou chave que saiu do catálogo não
    ///     derruba nada: o servidor sobe sem aquela opção.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];

            return stored
                .Where(p => ByKey.ContainsKey(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    // Nomes de fuso e de gerador: letras, dígitos e a pontuação que eles usam.
    private static bool IsToken(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '+' or '/' or ':' or '.');

    // Nomes de conta do Minecraft, separados por vírgula.
    private static bool IsNameList(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or ',' or ' ');
}
