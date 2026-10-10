using System.Globalization;

namespace TCMine.Server.Domain.Servers;

/// <summary>
///     O endereço que os jogadores usam para entrar num servidor — regras puras,
///     sem banco e sem rede.
///     Existe porque <see cref="GameServer.ConnectAddress" /> é texto livre e a
///     porta mora em <see cref="GameServer.GamePort" />: entregue cru, um endereço
///     sem ":porta" mandava o jogo para a 25565 mesmo quando o servidor publicava
///     outra. Foi assim que o botão Entrar do launcher falhou num servidor de
///     porta renumerada. Quem mostra ou envia o endereço passa por
///     <see cref="Resolve" />, e só por ele.
/// </summary>
public static class GameAddress
{
    private const int MaxPort = 65535;

    /// <summary>
    ///     O endereço publicado de um servidor. Do mais ao menos explícito:
    ///     texto com ":porta" sai como foi escrito — é o admin dizendo que a porta
    ///     de fora é outra (o roteador redireciona);
    ///     texto sem porta recebe a porta do servidor;
    ///     texto vazio e servidor com nome de DNS: o nome, SEM porta — quem a leva
    ///     é o registro SRV (ver <see cref="GameDns" />);
    ///     texto vazio sem nome de DNS é "automático": o host público da instalação
    ///     mais a porta do servidor. Sem host público conhecido não há endereço, e
    ///     volta vazio.
    /// </summary>
    /// <param name="dnsHost">
    ///     O <c>sub.dominio</c> do servidor (<see cref="GameDns.ServerHost" />), ou
    ///     nulo quando ele não tem.
    /// </param>
    public static string Resolve(string? connectAddress, int gamePort, string? publicHost, string? dnsHost = null)
    {
        var (host, port) = Split(connectAddress);

        if (host.Length == 0 && !string.IsNullOrWhiteSpace(dnsHost))
            return dnsHost.Trim();

        if (host.Length == 0)
            return Join(Split(publicHost).Host, gamePort);

        return port is null ? Join(host, gamePort) : connectAddress!.Trim();
    }

    /// <summary>A porta escrita no endereço, ou nulo quando ele não diz nenhuma.</summary>
    public static int? ExplicitPort(string? address) => Split(address).Port;

    /// <summary>
    ///     O mesmo host apontando para <paramref name="port" />. A porta padrão
    ///     fica implícita ("play.exemplo.com"), como os jogadores a digitam.
    ///     Endereço vazio continua vazio: sem host não há o que completar.
    /// </summary>
    public static string WithPort(string? address, int port) => Join(Split(address).Host, port);

    /// <summary>
    ///     Só um host: domínio ou IP, sem esquema, sem caminho, sem porta. É o que
    ///     o endereço público da instalação aceita — a porta é sempre a de cada
    ///     servidor, e um "http://" ali viraria parte do endereço do jogo.
    /// </summary>
    public static bool IsBareHost(string? text)
    {
        var value = text?.Trim() ?? "";

        return value.Length > 0
               && !value.Any(char.IsWhiteSpace)
               && !value.Contains('/')
               && !value.Contains('[')
               && !value.Contains(']')
               && Split(value).Port is null;
    }

    /// <summary>
    ///     Host e porta de um endereço. Os mesmos formatos que o launcher lê
    ///     (<c>ServerAddress.Parse</c>): "host", "host:porta", "[ipv6]" e
    ///     "[ipv6]:porta". Mais de um ":" sem colchetes é IPv6 sem porta — cortar
    ///     no último transformaria o fim do endereço numa porta.
    ///     O que vem depois do ":" e não é uma porta válida fica como parte do
    ///     host, intacto.
    /// </summary>
    public static (string Host, int? Port) Split(string? address)
    {
        var text = address?.Trim() ?? "";

        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']');
            if (close < 0)
                return (text, null);

            var bracketed = text[1..close];
            var rest = text[(close + 1)..];

            return rest.StartsWith(':') && TryPort(rest.AsSpan(1), out var v6Port)
                ? (bracketed, v6Port)
                : (bracketed, null);
        }

        var colon = text.LastIndexOf(':');

        if (colon <= 0 || text.IndexOf(':') != colon)
            return (text, null);

        return TryPort(text.AsSpan(colon + 1), out var port)
            ? (text[..colon], port)
            : (text, null);
    }

    private static string Join(string host, int port)
    {
        if (host.Length == 0)
            return "";

        if (port == GamePortDefaults.First)
            return host;

        // Um IPv6 só pode levar porta entre colchetes.
        var shown = host.Contains(':') ? "[" + host + "]" : host;

        return string.Create(CultureInfo.InvariantCulture, $"{shown}:{port}");
    }

    private static bool TryPort(ReadOnlySpan<char> text, out int port) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= MaxPort;
}
