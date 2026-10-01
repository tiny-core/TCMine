namespace TCMine.Server.Web.Components.Features.Modpacks;

public enum VersionChannel
{
    Alpha,
    Release
}

/// <summary>
///     Converte o número de versão do modpack entre string e (major, minor, patch,
///     canal), para o editor de 3 caixas. Só UI — o domínio guarda a string final.
///     Canal alpha vira sufixo "-alpha"; release fica sem sufixo.
/// </summary>
public static class PackVersion
{
    public static (int Major, int Minor, int Patch, VersionChannel Channel) Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (1, 0, 0, VersionChannel.Alpha);

        var channel = VersionChannel.Release;
        var core = value.Trim();

        var dash = core.IndexOf('-');
        if (dash >= 0)
        {
            if (core[(dash + 1)..].StartsWith("alpha", StringComparison.OrdinalIgnoreCase))
                channel = VersionChannel.Alpha;
            core = core[..dash];
        }

        var parts = core.Split('.');

        int At(int i)
        {
            return i < parts.Length && int.TryParse(parts[i], out var n) ? Math.Max(0, n) : 0;
        }

        return (At(0), At(1), At(2), channel);
    }

    public static string Format(int major, int minor, int patch, VersionChannel channel)
    {
        var core = $"{major}.{minor}.{patch}";
        return channel == VersionChannel.Alpha ? $"{core}-alpha" : core;
    }

    /// <summary>
    ///     Próxima versão sugerida: incrementa o patch e marca alpha.
    ///     Sem versão anterior (primeira do modpack), NÃO HÁ o que incrementar —
    ///     <see cref="Parse" /> devolve 1.0.0 como valor neutro para outros usos
    ///     (o editor de 3 caixas precisa de algo para mostrar vazio), mas aqui
    ///     tratá-lo como "última versão" sugeria 1.0.1-alpha para quem nunca
    ///     publicou nada — um número que não corresponde a nenhuma versão real.
    /// </summary>
    public static string SuggestNext(string? lastVersion)
    {
        if (string.IsNullOrWhiteSpace(lastVersion))
            return Format(1, 0, 0, VersionChannel.Alpha);

        var (major, minor, patch, _) = Parse(lastVersion);
        return Format(major, minor, patch + 1, VersionChannel.Alpha);
    }
}
