namespace TCMine_Server.Infrastructure.FileSystem;

/// <summary>
/// Centraliza todos os caminhos de diretório que o servidor precisa.
/// Garante que as pastas existem antes de qualquer serviço tentar usá-las.
/// Adicionar um novo diretório aqui é o único lugar que precisa ser alterado.
/// </summary>
public static class ServerPaths
{
    private const string DataDir = "tcmine-data";

    // Raiz de todos os dados do projeto (tcmine-data). Base das demais pastas; usada, por ex., para
    // medir o tamanho em disco ocupado só pelos dados do projeto.
    public static string Data(string root)
    {
        return Path.Combine(root, DataDir);
    }

    // Arquivos de release do launcher (feed Velopack + Setup.exe)
    public static string Updates(string root)
    {
        return Path.Combine(root, DataDir, "updates");
    }

    // Segredos persistidos em disco (imune à corrupção de env vars pelo Docker Compose)
    public static string Secrets(string root)
    {
        return Path.Combine(root, DataDir, "secrets");
    }

    // Raiz das instâncias de servidor Minecraft (cada instância num subdiretório {ID})
    public static string Servers(string root)
    {
        return Path.Combine(root, DataDir, "servers");
    }

    // Conteúdo dos modpacks por id (overrides descompactados, metadados). Editável pelo painel.
    public static string Modpacks(string root)
    {
        return Path.Combine(root, DataDir, "modpacks");
    }

    // Cache compartilhado dos jars de mods, por fileId. O launcher baixa daqui (não do CurseForge).
    public static string Mods(string root)
    {
        return Path.Combine(root, DataDir, "mods");
    }

    // Configs do jogador (keybinds/opções/minimapa), por (uuid, modpackId), como um zip em disco.
    // Um subdiretório por UUID; cada modpack é um {modpackId}.zip. Pode incluir o cache do mapa (grande).
    public static string PlayerConfigs(string root)
    {
        return Path.Combine(root, DataDir, "player-configs");
    }

    // Raiz do cache de runtime de servidor: loader/server instalados uma vez e compartilhados
    // entre instâncias (symlink/hardlink). Reduz drasticamente o disco com muitas instâncias.
    public static string ServerCache(string root)
    {
        return Path.Combine(root, DataDir, "server-cache");
    }

    // Instalações de loader+MC já montadas, por slug (ex.: neoforge-21.1.77-mc1.21.1). O grande
    // ganho de disco: o pesado libraries/ vive aqui uma vez; as instâncias só apontam para cá.
    public static string ServerCacheInstalled(string root)
    {
        return Path.Combine(ServerCache(root), "installed");
    }

    /// <summary>
    /// Cria todos os diretórios necessários para o servidor funcionar.
    /// Seguro chamar múltiplas vezes — não falha se a pasta já existir.
    /// </summary>
    public static void EnsureCreated(string root)
    {
        Directory.CreateDirectory(Updates(root));
        Directory.CreateDirectory(Secrets(root));
        Directory.CreateDirectory(Servers(root));
        Directory.CreateDirectory(Modpacks(root));
        Directory.CreateDirectory(Mods(root));
        Directory.CreateDirectory(PlayerConfigs(root));
        Directory.CreateDirectory(ServerCacheInstalled(root));
    }
}