using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>
///     A versão corrente do TCMine — <b>uma só</b> para o servidor e o launcher. Vem de
///     <c>SERVER_VERSION</c> (env embutida na imagem pelo GitHub Actions a partir da tag <c>v*</c>); em dev
///     (rodando do código) cai no <see cref="AssemblyInformationalVersionAttribute" /> do assembly.
///     Também oferece a comparação semver usada nos avisos de atualização.
/// </summary>
public static class AppVersion
{
    /// <summary>Versão corrente (ex.: "1.2.0"). Nunca lança; devolve "0.0.0" se nada for encontrado.</summary>
    public static string Current(IConfiguration config)
    {
        var configured = config["SERVER_VERSION"];
        if (!string.IsNullOrWhiteSpace(configured) && configured.Trim() != "0.0.0")
            return Clean(configured);

        var info = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
            return Clean(info);

        return "0.0.0";
    }

    /// <summary>É <paramref name="candidate" /> uma versão MAIOR que <paramref name="current" />?</summary>
    public static bool IsNewer(string? candidate, string? current)
    {
        return Compare(candidate, current) > 0;
    }

    /// <summary>Comparação semver simples (ignora sufixo de pré-lançamento). &lt;0, 0, &gt;0.</summary>
    public static int Compare(string? a, string? b)
    {
        var va = Parse(a);
        var vb = Parse(b);
        return va.CompareTo(vb);
    }

    private const string LocalTag = "-local.";

    /// <summary>
    /// Versão a empacotar ao (re)compilar o launcher, dada a <paramref name="releaseVersion"/> (última
    /// tag <c>launcher-v*</c>) e a <paramref name="feedVersion"/> já publicada no feed.
    /// <list type="bullet">
    /// <item>Release ainda não publicada (release &gt; feed) → build normal: <c>X.Y.Z</c>.</item>
    /// <item>Release já publicada (rebuild por config) → prerelease do próximo patch:
    /// <c>X.Y.(Z+1)-local.N</c>, que ordena <b>entre</b> a atual e a próxima release do GitHub. N
    /// incrementa a cada rebuild.</item>
    /// </list>
    /// </summary>
    public static string BuildVersion(string releaseVersion, string? feedVersion)
    {
        if (string.IsNullOrWhiteSpace(feedVersion)) return Clean(releaseVersion);

        var feed = Clean(feedVersion);
        var dash = feed.IndexOf(LocalTag, StringComparison.OrdinalIgnoreCase);
        var feedIsLocal = dash >= 0;
        var feedBase = feedIsLocal ? feed[..dash] : feed;

        var cmp = Parse(releaseVersion).CompareTo(Parse(feedBase));

        // Release genuinamente mais nova que o feed (inclui a release "cheia" superando o -local do mesmo
        // patch) → publica a release normal.
        if (cmp > 0 || (cmp == 0 && feedIsLocal)) return Clean(releaseVersion);

        // Rebuild por config: continua a série -local.N do feed, ou inicia no próximo patch.
        if (feedIsLocal && int.TryParse(feed[(dash + LocalTag.Length)..], out var n))
            return $"{feedBase}{LocalTag}{n + 1}";

        return $"{BumpPatch(feedBase)}{LocalTag}1";
    }

    // "1.0.1" → "1.0.2" (incrementa o patch). Robusto a 2 partes.
    private static string BumpPatch(string version)
    {
        var v = Parse(version);
        var patch = (v.Build < 0 ? 0 : v.Build) + 1;
        return $"{v.Major}.{v.Minor}.{patch}";
    }

    // "v1.2.0", "1.2.0+abc", "1.2.0-beta" → Version(1.2.0). Robusto a lixo → 0.0.0.
    private static Version Parse(string? raw)
    {
        var core = Clean(raw ?? string.Empty);
        var dash = core.IndexOf('-');
        if (dash >= 0) core = core[..dash]; // descarta pré-lançamento para a comparação numérica
        return Version.TryParse(core, out var v) ? v : new Version(0, 0, 0);
    }

    // Remove o 'v' inicial e o metadata de build ("+...")
    private static string Clean(string v)
    {
        var s = v.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        var plus = s.IndexOf('+');
        return plus >= 0 ? s[..plus] : s;
    }
}
