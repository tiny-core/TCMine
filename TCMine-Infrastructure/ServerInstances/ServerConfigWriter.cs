using System.Text;
using TCMine_Domain.Entities;

namespace TCMine_Infrastructure.ServerInstances;

/// <summary>
/// Gera/sincroniza os arquivos de configuração que o servidor Minecraft lê no diretório da instância:
/// <c>server.properties</c>, <c>eula.txt</c>, <c>user_jvm_args.txt</c> e as listas de jogadores
/// (<c>whitelist.json</c>, <c>ops.json</c>, <c>banned-players.json</c>, <c>banned-ips.json</c>).
///
/// Política de sincronização:
/// <list type="bullet">
/// <item><b>server.properties</b> — preserva o que o admin editou no painel (e chaves customizadas),
/// apenas sobrepondo as chaves que o TCMine governa (porta, MOTD, max-players, whitelist).</item>
/// <item><b>eula.txt / user_jvm_args.txt</b> — totalmente gerados pelo TCMine a cada provisão (a
/// fonte da verdade são os campos da instância: RAM, Xms, flags extras).</item>
/// <item><b>listas de jogadores</b> — só criadas vazias se ainda não existirem; o painel as edita
/// depois (Step 4). Nunca sobrescritas aqui.</item>
/// </list>
/// </summary>
public sealed class ServerConfigWriter
{
    /// <summary>Aplica todos os arquivos de config no diretório da instância.</summary>
    public void WriteAll(string instanceDir, ServerInstanceEntity instance)
    {
        Directory.CreateDirectory(instanceDir);
        WriteEula(instanceDir);
        WriteJvmArgs(instanceDir, instance);
        SyncServerProperties(instanceDir, instance);
        EnsurePlayerLists(instanceDir);
    }

    // ── eula.txt ──────────────────────────────────────────────────────────────────────────────────

    // O servidor recusa subir sem o EULA aceito; o admin já aceita ao criar a instância no painel.
    private static void WriteEula(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "eula.txt"), "eula=true\n");
    }

    // ── user_jvm_args.txt ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Escreve <c>user_jvm_args.txt</c> (formato de arquivo de argumentos da JVM: uma flag por linha,
    /// <c>#</c> = comentário). O loader (NeoForge/Forge) referencia este arquivo via <c>@user_jvm_args.txt</c>.
    /// Memória vem de <see cref="ServerInstanceEntity.RamMb"/> (Xmx) e <c>XmsMb</c> (0 = igual ao Xmx).
    /// </summary>
    private static void WriteJvmArgs(string dir, ServerInstanceEntity instance)
    {
        var xms = instance.XmsMb > 0 ? instance.XmsMb : instance.RamMb;

        var sb = new StringBuilder();
        sb.AppendLine("# Gerado pelo TCMine — memória vem dos campos da instância; flags extras do painel.");
        sb.AppendLine($"-Xms{xms}M");
        sb.AppendLine($"-Xmx{instance.RamMb}M");

        // Flags extras (uma por linha no campo da instância): copia as não-vazias, ignorando comentários
        foreach (var raw in instance.ExtraJvmArgs.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var flag = raw.Trim();
            if (flag.Length > 0 && !flag.StartsWith('#')) sb.AppendLine(flag);
        }

        File.WriteAllText(Path.Combine(dir, "user_jvm_args.txt"), sb.ToString());
    }

    // ── server.properties ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lê o <c>server.properties</c> atual (se houver), sobrepõe as chaves governadas pelo TCMine e
    /// regrava — preservando as edições do admin e quaisquer chaves customizadas.
    /// </summary>
    private static void SyncServerProperties(string dir, ServerInstanceEntity instance)
    {
        var path = Path.Combine(dir, "server.properties");
        var props = File.Exists(path) ? ReadProperties(path) : DefaultProperties();

        // Chaves governadas pelo TCMine (a instância é a fonte da verdade destas)
        Set(props, "server-port", instance.Port.ToString());
        Set(props, "query.port", instance.Port.ToString());
        Set(props, "max-players", instance.MaxPlayers.ToString());
        Set(props, "motd", instance.Motd);

        WriteProperties(path, props);
    }

    // Conjunto mínimo de defaults sensatos para uma primeira provisão (sem arquivo prévio)
    private static List<KeyValuePair<string, string>> DefaultProperties()
    {
        return
        [
            new("online-mode", "true"),
            new("white-list", "false"),
            new("enforce-whitelist", "false"),
            new("pvp", "true"),
            new("difficulty", "normal"),
            new("gamemode", "survival"),
            new("spawn-protection", "0"),
            new("view-distance", "10"),
            new("simulation-distance", "10")
        ];
    }

    // Atualiza a chave in-place (preservando a posição) ou anexa ao fim se for nova
    private static void Set(List<KeyValuePair<string, string>> props, string key, string value)
    {
        var idx = props.FindIndex(p => p.Key == key);
        if (idx >= 0) props[idx] = new KeyValuePair<string, string>(key, value);
        else props.Add(new KeyValuePair<string, string>(key, value));
    }

    // Parser simples de .properties: ignora comentários (#/!) e linhas sem '='; preserva a ordem
    private static List<KeyValuePair<string, string>> ReadProperties(string path)
    {
        var result = new List<KeyValuePair<string, string>>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] is '#' or '!') continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            result.Add(new KeyValuePair<string, string>(line[..eq].Trim(), line[(eq + 1)..]));
        }

        return result;
    }

    private static void WriteProperties(string path, List<KeyValuePair<string, string>> props)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Minecraft server properties — gerenciado pelo TCMine (editável no painel)");
        foreach (var (key, value) in props) sb.Append(key).Append('=').AppendLine(value);
        File.WriteAllText(path, sb.ToString());
    }

    // ── Listas de jogadores ─────────────────────────────────────────────────────────────────────────

    // Cria os JSONs de jogador vazios se ainda não existirem (o painel os edita depois). O servidor
    // espera um array JSON; ausência faz alguns loaders reclamarem no boot.
    private static void EnsurePlayerLists(string dir)
    {
        foreach (var name in new[] { "whitelist.json", "ops.json", "banned-players.json", "banned-ips.json" })
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) File.WriteAllText(path, "[]\n");
        }
    }
}
