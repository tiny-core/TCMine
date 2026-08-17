using System.Text.RegularExpressions;

namespace TCMine.Server.Application.Common;

/// <summary>
///     Extrai a contagem de jogadores da resposta do comando <c>list</c>.
///     Fica em Common, e não junto dos casos de uso de servidor, por duas
///     razões: é função pura, testável sem nada em volta, e o namespace dos
///     casos de uso exige consulta de papel (ver AuthorizationRules) — coisa que
///     um parser não faz.
///     O formato NÃO é contrato: é texto de log que muda entre vanilla, Paper e
///     mods que reescrevem o comando. Por isso duas tentativas e um nulo
///     honesto no fim, em vez de zero — zero significaria "servidor vazio", e
///     dizer isso quando não sabemos seria pior que não dizer nada.
/// </summary>
public static partial class PlayerListParser
{
    public static int? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        // "There are 3 of a max of 20 players online" (vanilla) e
        // "There are 3/20 players online" (variações de Paper e mods).
        var comMaximo = ComMaximo().Match(output);
        if (comMaximo.Success && int.TryParse(comMaximo.Groups["online"].Value, out var online))
            return online;

        // Último recurso: o primeiro número depois de "there are". Cobre
        // traduções do sufixo sem cobrir a frase inteira.
        var soOnline = SoOnline().Match(output);
        return soOnline.Success && int.TryParse(soOnline.Groups["online"].Value, out var n)
            ? n
            : null;
    }

    [GeneratedRegex(
        @"(?<online>\d+)\s*(?:/|of\s+a\s+max\s+of)\s*(?<max>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ComMaximo();

    [GeneratedRegex(
        @"there\s+are\s+(?<online>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SoOnline();
}
