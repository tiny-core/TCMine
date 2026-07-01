using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using TCMine_Server.Infrastructure.FileSystem;

namespace TCMine_Server.Infrastructure.Launcher;

/// <summary>
/// Inspeciona a pasta do feed Velopack (<c>tcmine-data/updates</c>) para descobrir a versão mais
/// recente do launcher e o instalador de primeira instalação (<c>*Setup.exe</c>).
///
/// O feed em si (RELEASES, releases.*.json, *-full/-delta.nupkg, Setup.exe) é servido como
/// arquivos estáticos em <c>/updates</c> — é o Velopack do cliente que o consome. Este serviço só
/// existe para a Home mostrar "Baixar (versão X)" ou "em breve", e para o atalho <c>/download</c>.
///
/// Singleton, mas lê o disco a cada chamada (sem cache): publicar uma nova versão é colocar
/// arquivos na pasta, e queremos refletir isso sem reiniciar o servidor.
/// </summary>
public sealed partial class LauncherFeedService(IHostEnvironment env)
{
    // Extrai a versão de um nupkg full: "TCMine-Launcher-1.2.3-full.nupkg" → "1.2.3".
    private static readonly Regex FullPackage = FullPackageRegex();

    private readonly string _updatesDir = ServerPaths.Updates(env.ContentRootPath);

    /// <summary>Versão mais recente publicada no feed, ou null se ainda nada foi compilado.</summary>
    public string? LatestVersion()
    {
        if (!Directory.Exists(_updatesDir)) return null;

        return Directory.EnumerateFiles(_updatesDir, "*-full.nupkg")
            .Select(f => FullPackage.Match(Path.GetFileName(f)))
            .Where(m => m.Success)
            .Select(m => m.Groups["v"].Value)
            // Ordena por versão semântica quando possível; caso contrário, ordem textual.
            .OrderByDescending(v => Version.TryParse(StripSuffix(v), out var ver) ? ver : new Version(0, 0))
            .ThenByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Há um instalador (<c>*Setup.exe</c>) pronto para a primeira instalação?</summary>
    public bool HasInstaller()
    {
        return LatestSetupExe() is not null;
    }

    /// <summary>Caminho do <c>*Setup.exe</c> mais recente, ou null se ainda não há.</summary>
    public FileInfo? LatestSetupExe()
    {
        if (!Directory.Exists(_updatesDir)) return null;

        return new DirectoryInfo(_updatesDir)
            .GetFiles("*Setup.exe")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    // Descarta o sufixo de pré-lançamento ("1.2.3-beta" → "1.2.3") para a comparação numérica.
    private static string StripSuffix(string v)
    {
        var dash = v.IndexOf('-');
        return dash >= 0 ? v[..dash] : v;
    }

    [GeneratedRegex(@"-(?<v>\d+\.\d+\.\d+[0-9A-Za-z.\-]*)-full\.nupkg$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "pt-BR")]
    private static partial Regex FullPackageRegex();
}