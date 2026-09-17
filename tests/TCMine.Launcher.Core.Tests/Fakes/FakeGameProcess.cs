using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>
///     Um jogo que o teste controla: escreve quando mandado, fecha quando mandado.
///     Fica aberto até <see cref="Terminar" />, e isso é o essencial — um falso
///     que fechasse assim que ligado tornaria impossível testar qualquer coisa
///     sobre o estado "a jogar", porque ele acabaria antes da asserção.
/// </summary>
public sealed class FakeGameProcess : IGameProcess
{
    private readonly Channel<string> _linhas = Channel.CreateUnbounded<string>();

    public int CodigoDeSaida { get; init; }

    /// <summary>Atirado depois da primeira linha, para o caminho de falha.</summary>
    public Exception? Erro { get; init; }

    public bool Morto { get; private set; }

    public int ExitCode => CodigoDeSaida;

    public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var linha in _linhas.Reader.ReadAllAsync(ct))
        {
            yield return linha;

            if (Erro is not null)
                throw Erro;
        }
    }

    public void Kill()
    {
        Morto = true;
        Terminar();
    }

    public void Dispose() { }

    public void Escrever(string linha) => _linhas.Writer.TryWrite(linha);

    public void Terminar() => _linhas.Writer.TryComplete();
}
