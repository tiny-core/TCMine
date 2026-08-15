using System.Globalization;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Decide se uma versão de loader satisfaz o que um mod exige.
///     Função pura, e com um viés deliberado: <b>na dúvida, aceita</b>. Uma
///     recusa errada bloqueia um mod que funcionaria e o admin não tem como
///     contornar; um aceite errado só devolve o comportamento que já existia
///     antes desta checagem. Por isso tudo o que não for uma exigência clara e
///     violada resulta em "pode instalar".
/// </summary>
public static class LoaderVersionRange
{
    /// <summary>
    ///     Satisfaz? <paramref name="range" /> vazio, ilegível ou em formato
    ///     desconhecido devolve true.
    /// </summary>
    public static bool IsSatisfied(string? range, string? loaderVersion)
    {
        if (string.IsNullOrWhiteSpace(range) || string.IsNullOrWhiteSpace(loaderVersion))
            return true;

        var atual = Parse(loaderVersion);
        if (atual is null)
            return true;

        var texto = range.Trim();

        // Notação do Fabric: ">=0.15.0", ">0.15", "*", "1.2.x". Só o >= e o >
        // dizem algo verificável; o resto passa.
        if (texto.StartsWith(">=", StringComparison.Ordinal))
            return AtLeast(atual, texto[2..], inclusive: true);

        if (texto.StartsWith('>'))
            return AtLeast(atual, texto[1..], inclusive: false);

        // Notação Maven do Forge/NeoForge: "[21.1.80,)", "[1.0,2.0)", "(1.0,2.0]".
        if (texto.StartsWith('[') || texto.StartsWith('('))
            return InMavenRange(atual, texto);

        // Versão solta ("21.1.80") no Forge é preferência, não exigência —
        // tratá-la como mínimo recusaria mods que funcionam.
        return true;
    }

    private static bool AtLeast(int[] atual, string minimoTexto, bool inclusive)
    {
        var minimo = Parse(minimoTexto);
        if (minimo is null)
            return true;

        var cmp = Compare(atual, minimo);
        return inclusive ? cmp >= 0 : cmp > 0;
    }

    private static bool InMavenRange(int[] atual, string texto)
    {
        var fechaInicio = texto[0] is '[';
        var fechaFim = texto[^1] is ']';

        if (texto.Length < 3 || (texto[^1] is not (']' or ')')))
            return true;

        var miolo = texto[1..^1];
        var partes = miolo.Split(',');

        // "[1.0]" — versão exata.
        if (partes.Length is 1)
        {
            var exata = Parse(partes[0]);
            return exata is null || Compare(atual, exata) is 0;
        }

        if (partes.Length is not 2)
            return true;

        var minimo = Parse(partes[0]);
        var maximo = Parse(partes[1]);

        if (minimo is not null)
        {
            var cmp = Compare(atual, minimo);
            if (fechaInicio ? cmp < 0 : cmp <= 0)
                return false;
        }

        if (maximo is not null)
        {
            var cmp = Compare(atual, maximo);
            if (fechaFim ? cmp > 0 : cmp >= 0)
                return false;
        }

        return true;
    }

    /// <summary>
    ///     "21.1.80-beta" → [21, 1, 80]. Ignora o sufixo: comparar pré-lançamento
    ///     corretamente exigiria SemVer completo, e loader raramente usa.
    /// </summary>
    private static int[]? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpo = texto.Trim();
        var corte = limpo.IndexOfAny(['-', '+']);
        if (corte > 0)
            limpo = limpo[..corte];

        var partes = limpo.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length is 0)
            return null;

        var numeros = new int[partes.Length];
        for (var i = 0; i < partes.Length; i++)
        {
            if (!int.TryParse(partes[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numeros[i]))
                return null; // tem letra no meio: não sabemos comparar
        }

        return numeros;
    }

    /// <summary>Compara segmento a segmento; o que faltar conta como zero.</summary>
    private static int Compare(int[] a, int[] b)
    {
        var tamanho = Math.Max(a.Length, b.Length);

        for (var i = 0; i < tamanho; i++)
        {
            var esquerda = i < a.Length ? a[i] : 0;
            var direita = i < b.Length ? b[i] : 0;

            if (esquerda != direita)
                return esquerda.CompareTo(direita);
        }

        return 0;
    }
}
