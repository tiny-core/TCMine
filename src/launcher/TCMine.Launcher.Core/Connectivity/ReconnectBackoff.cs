namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Quanto esperar antes de cada nova tentativa de ligação.
///     Sem limite de tentativas, de propósito. A política anterior desistia
///     depois de cinco (menos de um minuto), e um servidor a reiniciar por
///     atualização demora mais do que isso: o launcher ficava mudo até ser
///     fechado e reaberto.
///     O teto de 30 s é o que impede "para sempre" de virar um martelo: com o
///     servidor fora do ar, cada launcher aberto faz no máximo dois pedidos por
///     minuto.
///     Um lugar só para a regra porque dois laços a usam — a reconexão do
///     SignalR e a reabertura depois de uma queda definitiva — e duas tabelas
///     divergiriam na primeira afinação.
/// </summary>
public static class ReconnectBackoff
{
    private static readonly TimeSpan[] Steps =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    public static TimeSpan Ceiling => Steps[^1];

    /// <summary>
    ///     A espera antes da tentativa seguinte, dado quantas já falharam.
    ///     Zero falhas é a primeira tentativa, imediata: a maior parte das
    ///     quedas é um soluço de rede que já passou.
    /// </summary>
    public static TimeSpan DelayFor(long previousFailures) =>
        Steps[(int)Math.Clamp(previousFailures, 0, Steps.Length - 1)];
}
