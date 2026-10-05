namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     O endereço de um servidor de jogo, separado em host e porta como o
///     Minecraft os pede (<c>--quickPlayMultiplayer host:porta</c>, ou
///     <c>--server</c>/<c>--port</c> nas versões antigas).
///     O <c>ConnectAddress</c> que o servidor TCMine devolve é o mesmo texto que o
///     jogador digitaria no "Adicionar servidor": host, com ou sem porta, e IPv6
///     entre colchetes quando leva porta.
/// </summary>
public sealed record ServerAddress(string Host, int? Port)
{
    /// <summary>O endereço, ou nulo quando não há o que entrar.</summary>
    public static ServerAddress? Parse(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var text = address.Trim();

        // [::1]:25565 — o único formato em que um IPv6 pode levar porta.
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']');
            if (close < 0)
                return null;

            var host = text[1..close];
            var rest = text[(close + 1)..];

            if (rest.Length is 0)
                return new ServerAddress(host, null);

            return rest.StartsWith(':') && TryPort(rest[1..], out var v6Port)
                ? new ServerAddress(host, v6Port)
                : null;
        }

        var colon = text.LastIndexOf(':');

        // Mais de um ':' sem colchetes é IPv6 sem porta: cortar no último
        // transformaria o fim do endereço numa porta.
        if (colon < 0 || text.IndexOf(':') != colon)
            return new ServerAddress(text, null);

        return TryPort(text[(colon + 1)..], out var port) && colon > 0
            ? new ServerAddress(text[..colon], port)
            : null;
    }

    private static bool TryPort(string text, out int port) =>
        int.TryParse(text, System.Globalization.NumberStyles.None, null, out port) && port is > 0 and <= 65535;
}
