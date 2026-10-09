using System.Globalization;

namespace TCMine.Server.Domain.Servers;

/// <summary>
///     Regras puras da porta de jogo — sem banco e sem Docker, para serem
///     testadas sozinhas.
/// </summary>
public static class GamePortRange
{
    /// <summary>Abaixo de 1024 são portas privilegiadas: o Docker pode recusar.</summary>
    public const int Min = 1024;

    public const int Max = 65535;

    public static bool IsValid(int port) => port is >= Min and <= Max;

    /// <summary>
    ///     Uma faixa utilizável a partir do que está nas configurações. Valores
    ///     fora dos limites ou invertidos caem na faixa padrão: uma configuração
    ///     ruim não pode impedir a criação de servidores.
    /// </summary>
    public static (int Start, int End) Normalize(int start, int end) =>
        IsValid(start) && IsValid(end) && start <= end
            ? (start, end)
            : (GamePortDefaults.First, GamePortDefaults.Last);

    /// <summary>A menor porta da faixa que não está em <paramref name="used" />, ou nulo se não sobra nenhuma.</summary>
    public static int? FirstFree(int start, int end, IReadOnlySet<int> used)
    {
        for (var port = start; port <= end; port++)
        {
            if (!used.Contains(port))
                return port;
        }

        return null;
    }

    /// <summary>
    ///     A porta que um endereço de conexão aponta. Sem ":porta" o cliente do
    ///     Minecraft usa a padrão, então é ela que vale.
    /// </summary>
    public static int AddressPort(string? address)
    {
        var (_, port) = Split(address);
        return port ?? GamePortDefaults.First;
    }

    /// <summary>
    ///     O mesmo endereço apontando para <paramref name="port" />. A porta padrão
    ///     fica implícita ("play.exemplo.com"), como os jogadores a digitam.
    ///     Endereço vazio continua vazio: sem host não há o que completar.
    /// </summary>
    public static string WithPort(string? address, int port)
    {
        var (host, _) = Split(address);
        if (host.Length == 0)
            return "";

        return port == GamePortDefaults.First
            ? host
            : string.Create(CultureInfo.InvariantCulture, $"{host}:{port}");
    }

    // Só o que vem depois do ÚLTIMO ":" e é número conta como porta; qualquer
    // outra coisa é parte do host e fica intacta.
    private static (string Host, int? Port) Split(string? address)
    {
        var text = address?.Trim() ?? "";
        var colon = text.LastIndexOf(':');

        return colon >= 0
               && int.TryParse(text.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
               && port > 0
            ? (text[..colon], port)
            : (text, null);
    }
}
