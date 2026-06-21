namespace TCMine_Core.Infrastructure;

/// <summary>
/// Centraliza todos os caminhos de diretório que o servidor precisa.
/// Garante que as pastas existem antes de qualquer serviço tentar usá-las.
/// Adicionar um novo diretório aqui é o único lugar que precisa ser alterado.
/// </summary>
public static class ServerPaths
{
    // Arquivos de release do launcher (feed Velopack + Setup.exe)
    public static string Updates(string root)
    {
        return Path.Combine(root, "updates");
    }

    // Segredos persistidos em disco (imune à corrupção de env vars pelo Docker Compose)
    public static string Secrets(string root)
    {
        return Path.Combine(root, "secrets");
    }

    // Raiz das instâncias de servidor Minecraft (cada instância num subdiretório {Id})
    public static string Servers(string root)
    {
        return Path.Combine(root, "servers");
    }

    // Conteúdo dos modpacks por id (overrides descompactados, metadados). Editável pelo painel.
    public static string Modpacks(string root)
    {
        return Path.Combine(root, "modpacks");
    }

    // Cache compartilhado dos jars de mods, por fileId. O launcher baixa daqui (não do CurseForge).
    public static string Mods(string root)
    {
        return Path.Combine(root, "mods");
    }

    /// <summary>
    /// Cria todos os diretórios necessários para o servidor funcionar.
    /// Seguro chamar múltiplas vezes — não falha se a pasta já existir.
    /// </summary>
    public static void EnsureCreated(string root, string? subDir = "tcmine-data")
    {
        root = Path.Combine(root, subDir ?? "tcmine-data");

        Directory.CreateDirectory(Updates(root));
        Directory.CreateDirectory(Secrets(root));
        Directory.CreateDirectory(Servers(root));
        Directory.CreateDirectory(Modpacks(root));
        Directory.CreateDirectory(Mods(root));
    }
}