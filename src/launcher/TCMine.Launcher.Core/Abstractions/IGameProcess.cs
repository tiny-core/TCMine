namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     O jogo a correr, depois de aberto.
///     Existe porque abrir o processo não é o fim: enquanto ele vive, o launcher
///     precisa de saber que está vivo (para não abrir uma segunda cópia sobre o
///     mesmo mundo) e de conseguir mostrar o que ele escreve (para um crash ser
///     diagnosticável em vez de "fechou sozinho").
///     Uma stream só, e não eventos: <c>await foreach</c> acaba quando o jogo
///     fecha, o que torna "terminou" e "eis a última linha" a mesma coisa — com
///     eventos seriam dois sinais a chegar fora de ordem.
/// </summary>
public interface IGameProcess : IDisposable
{
    /// <summary>
    ///     Linhas de stdout e stderr, na ordem em que o jogo as escreve.
    ///     Os dois canais vêm misturados de propósito: separá-los faria as
    ///     exceções aparecerem longe do que as causou, que é o contrário de um log.
    ///     Termina quando o processo sai. Consumir uma vez só.
    /// </summary>
    IAsyncEnumerable<string> ReadOutputAsync(CancellationToken ct);

    /// <summary>Válido depois de <see cref="ReadOutputAsync" /> terminar.</summary>
    int ExitCode { get; }

    /// <summary>Mata o processo. Para quando o jogo pendura e não há mais saída.</summary>
    void Kill();
}
