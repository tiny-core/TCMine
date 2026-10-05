namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Ordem entre dois números de versão de um modpack, por SemVer.
///     Pelo NÚMERO e não pela data de criação: um hotfix 1.0.1 publicado depois
///     da 1.1.0 é mais antigo que ela, e trocar uma instância da 1.1.0 por ele
///     seria descer de versão — o que parte os mundos (CLAUDE.md §7.0).
/// </summary>
public static class ModpackVersionOrder
{
    /// <summary>
    ///     Negativo se <paramref name="a" /> é anterior, positivo se posterior,
    ///     zero se iguais; nulo quando algum dos dois não é um número de versão
    ///     — e aí quem chama decide pela opção segura.
    /// </summary>
    public static int? Compare(string a, string b)
    {
        if (Parse(a) is not { } x || Parse(b) is not { } y)
            return null;

        for (var i = 0; i < Math.Max(x.Numbers.Length, y.Numbers.Length); i++)
        {
            var n = i < x.Numbers.Length ? x.Numbers[i] : 0;
            var m = i < y.Numbers.Length ? y.Numbers[i] : 0;

            if (n != m)
                return n.CompareTo(m);
        }

        // Mesmo núcleo: a estável vem DEPOIS de qualquer pré-lançamento dela
        // (1.2.0-alpha.3 < 1.2.0), como manda o SemVer.
        return (x.PreRelease, y.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            var (p, q) => ComparePreRelease(p, q)
        };
    }

    private static (long[] Numbers, string? PreRelease)? Parse(string version)
    {
        var text = version.Trim().TrimStart('v', 'V');

        // Metadados de build (+abc) não entram na ordem.
        var plus = text.IndexOf('+');
        if (plus >= 0)
            text = text[..plus];

        var dash = text.IndexOf('-');
        var core = dash >= 0 ? text[..dash] : text;
        var pre = dash >= 0 ? text[(dash + 1)..] : null;

        var parts = core.Split('.');
        var numbers = new long[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!long.TryParse(parts[i], System.Globalization.NumberStyles.None, null, out numbers[i]))
                return null;
        }

        return (numbers, string.IsNullOrEmpty(pre) ? null : pre);
    }

    /// <summary>Identificador a identificador: numéricos por valor, os outros por texto.</summary>
    private static int ComparePreRelease(string a, string b)
    {
        var x = a.Split('.');
        var y = b.Split('.');

        for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            var xNum = long.TryParse(x[i], out var xn);
            var yNum = long.TryParse(y[i], out var yn);

            var result = (xNum, yNum) switch
            {
                (true, true) => xn.CompareTo(yn),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(x[i], y[i])
            };

            if (result != 0)
                return Math.Sign(result);
        }

        return x.Length.CompareTo(y.Length);
    }
}
