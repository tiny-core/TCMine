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
}
