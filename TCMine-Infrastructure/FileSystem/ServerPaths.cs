namespace TCMine_Infrastructure.FileSystem;

/// <summary>
/// Centraliza todos os caminhos de diretório que o servidor precisa.
/// Garante que as pastas existem antes de qualquer serviço tentar usá-las.
/// Adicionar um novo diretório aqui é o único lugar que precisa ser alterado.
/// </summary>
public static class ServerPaths
{
    private const string DataDir = "tcmine-data";

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
    }
}