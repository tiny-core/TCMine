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
