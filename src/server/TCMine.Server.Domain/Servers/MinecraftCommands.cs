namespace TCMine.Server.Domain.Servers;

/// <summary>Como a referência de comandos agrupa o catálogo na tela.</summary>
public enum MinecraftCommandCategory
{
    Players,
    Moderation,
    World,
    ItemsAndEffects,
    Server
}

/// <summary>
///     Um comando do console, como a referência o mostra.
///     <see cref="Syntax" /> segue a notação da wiki do Minecraft: &lt;obrigatório&gt;,
///     [opcional] e a|b para alternativas.
///     <see cref="Destructive" /> marca o que não tem volta ou afeta todos os
///     jogadores — a tela pede atenção antes de mandar, mas quem decide se pode
///     é o <c>ConsoleCommandPolicy</c>, não este catálogo.
/// </summary>
public sealed record MinecraftCommand(
    string Name,
    string Syntax,
    string Description,
    MinecraftCommandCategory Category,
    bool Destructive = false);

/// <summary>
///     Os comandos do Minecraft Java que um dono de servidor mais usa pelo
///     console. É uma REFERÊNCIA, não uma allowlist: o servidor aceita qualquer
///     comando (inclusive os que os mods acrescentam), e o que cada papel pode
///     mandar continua decidido pelo <c>ConsoleCommandPolicy</c>.
///     Sem a barra inicial: pelo console (e pelo RCON) os comandos vão sem ela.
/// </summary>
public static class MinecraftCommands
{
    public static IReadOnlyList<MinecraftCommand> All { get; } =
    [
        // ---------- Jogadores ----------
        new("list", "list [uuids]", "Mostra quem está online.", MinecraftCommandCategory.Players),
        new("say", "say <mensagem>", "Manda uma mensagem a todos, assinada pelo servidor.",
            MinecraftCommandCategory.Players),
        new("msg", "msg <jogador> <mensagem>", "Mensagem privada para um jogador.", MinecraftCommandCategory.Players),
        new("tellraw", "tellraw <alvo> <json>", "Mensagem com cores e links, em JSON de texto.",
            MinecraftCommandCategory.Players),
        new("title", "title <alvo> title|subtitle|actionbar <json>", "Texto grande no meio da tela dos jogadores.",
            MinecraftCommandCategory.Players),
        new("tp", "tp <jogador> <destino>|<x> <y> <z>", "Teleporta um jogador até outro ou até coordenadas.",
            MinecraftCommandCategory.Players),
        new("gamemode", "gamemode survival|creative|adventure|spectator [jogador]", "Troca o modo de jogo.",
            MinecraftCommandCategory.Players),
        new("spawnpoint", "spawnpoint [jogador] [x y z]", "Define onde o jogador renasce.",
            MinecraftCommandCategory.Players),
        new("xp", "xp add|set <jogador> <quantidade> [points|levels]", "Dá ou ajusta experiência.",
            MinecraftCommandCategory.Players),
        new("kill", "kill [alvo]", "Mata o alvo. Sem alvo, mata o próprio executor.", MinecraftCommandCategory.Players,
            true),

        // ---------- Moderação ----------
        new("kick", "kick <jogador> [motivo]", "Desconecta o jogador; ele pode voltar.",
            MinecraftCommandCategory.Moderation),
        new("ban", "ban <jogador> [motivo]", "Bane o jogador até um pardon.", MinecraftCommandCategory.Moderation,
            true),
        new("ban-ip", "ban-ip <ip|jogador> [motivo]", "Bane o endereço IP.", MinecraftCommandCategory.Moderation, true),
        new("pardon", "pardon <jogador>", "Desfaz o banimento de um jogador.", MinecraftCommandCategory.Moderation),
        new("pardon-ip", "pardon-ip <ip>", "Desfaz o banimento de um IP.", MinecraftCommandCategory.Moderation),
        new("banlist", "banlist [players|ips]", "Lista quem está banido.", MinecraftCommandCategory.Moderation),
        new("whitelist", "whitelist add|remove|list|on|off|reload [jogador]",
            "Gerencia a lista de quem pode entrar. O TCMine também a sincroniza com os membros.",
            MinecraftCommandCategory.Moderation),
        new("op", "op <jogador>", "Dá poder de operador no jogo — acesso a todos os comandos.",
            MinecraftCommandCategory.Moderation, true),
        new("deop", "deop <jogador>", "Tira o poder de operador.", MinecraftCommandCategory.Moderation),

        // ---------- Mundo ----------
        new("time", "time set day|night|noon|midnight|<ticks>", "Muda a hora do dia.", MinecraftCommandCategory.World),
        new("weather", "weather clear|rain|thunder [duração]", "Muda o tempo.", MinecraftCommandCategory.World),
        new("difficulty", "difficulty peaceful|easy|normal|hard", "Muda a dificuldade.",
            MinecraftCommandCategory.World),
        new("gamerule", "gamerule <regra> [valor]", "Lê ou altera uma regra (keepInventory, doDaylightCycle…).",
            MinecraftCommandCategory.World),
        new("setworldspawn", "setworldspawn [x y z]", "Define o ponto de nascimento do mundo.",
            MinecraftCommandCategory.World),
        new("worldborder", "worldborder set|add|center <…>", "Ajusta a borda do mundo.",
            MinecraftCommandCategory.World),
        new("locate", "locate structure|biome <id>", "Encontra a estrutura ou o bioma mais próximo.",
            MinecraftCommandCategory.World),
        new("seed", "seed", "Mostra a semente do mundo.", MinecraftCommandCategory.World),
        new("forceload", "forceload add|remove|query <x> <z>", "Mantém chunks sempre carregados.",
            MinecraftCommandCategory.World),
        new("summon", "summon <entidade> [x y z]", "Cria uma entidade.", MinecraftCommandCategory.World),
        new("setblock", "setblock <x> <y> <z> <bloco>", "Coloca um bloco.", MinecraftCommandCategory.World, true),
        new("fill", "fill <x1> <y1> <z1> <x2> <y2> <z2> <bloco>", "Preenche uma área com um bloco.",
            MinecraftCommandCategory.World, true),

        // ---------- Itens e efeitos ----------
        new("give", "give <jogador> <item> [quantidade]", "Dá um item.", MinecraftCommandCategory.ItemsAndEffects),
        new("clear", "clear [jogador] [item] [quantidade]", "Remove itens do inventário.",
            MinecraftCommandCategory.ItemsAndEffects, true),
        new("effect", "effect give|clear <alvo> [efeito] [segundos] [nível]", "Aplica ou remove efeitos.",
            MinecraftCommandCategory.ItemsAndEffects),
        new("enchant", "enchant <jogador> <encantamento> [nível]", "Encanta o item na mão do jogador.",
            MinecraftCommandCategory.ItemsAndEffects),

        // ---------- Servidor ----------
        new("save-all", "save-all [flush]", "Grava o mundo no disco agora.", MinecraftCommandCategory.Server),
        new("save-off", "save-off", "Desliga o salvamento automático. Lembre de ligar de volta com save-on.",
            MinecraftCommandCategory.Server, true),
        new("save-on", "save-on", "Liga o salvamento automático.", MinecraftCommandCategory.Server),
        new("reload", "reload", "Recarrega datapacks, receitas e loot tables.", MinecraftCommandCategory.Server),
        new("tick", "tick query|rate <tps>|freeze|unfreeze", "Consulta ou ajusta o ritmo do jogo (1.20.3+).",
            MinecraftCommandCategory.Server),
        new("stop", "stop", "Desliga o servidor. Pelo TCMine, use o botão Parar.", MinecraftCommandCategory.Server,
            true)
    ];

    /// <summary>O comando pelo nome, sem diferenciar maiúsculas; nulo se não está no catálogo.</summary>
    public static MinecraftCommand? Find(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
