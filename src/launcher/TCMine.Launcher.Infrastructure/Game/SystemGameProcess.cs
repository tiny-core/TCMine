using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Um <see cref="Process" /> do sistema por trás do <see cref="IGameProcess" />.
///     Junta stdout e stderr num canal só, na ordem de chegada. Separá-los faria
///     a exceção aparecer longe da linha que a causou, que é o contrário do que
///     um log serve para fazer.
///     O canal é ilimitado de propósito: aplicar o limite aqui faria o jogo
///     bloquear à espera de que o launcher lesse — a política de quantas linhas
///     guardar é da <c>GameSession</c>, que descarta as velhas sem nunca segurar
///     quem escreve.
/// </summary>
internal sealed class SystemGameProcess : IGameProcess
{
    private readonly Channel<string> _linhas = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Process _processo;

    public SystemGameProcess(Process process)
    {
        _processo = process;

        _processo.OutputDataReceived += AoReceber;
        _processo.ErrorDataReceived += AoReceber;
    }

    public int ExitCode => _processo.ExitCode;

    /// <summary>
    ///     Começa a bombear e devolve as linhas até o jogo fechar.
    ///     O <c>BeginOutputReadLine</c> fica aqui, e não no construtor, porque só
    ///     pode ser chamado depois do <c>Start</c> — e chamá-lo duas vezes atira.
    /// </summary>
    public async IAsyncEnumerable<string> ReadOutputAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        _processo.BeginOutputReadLine();
        _processo.BeginErrorReadLine();

        // Fecha o canal quando o jogo sai. Sem isto o await foreach de quem lê
        // nunca termina, e a sessão ficaria eternamente "a jogar".
        _ = FecharAoSairAsync();

        await foreach (var line in _linhas.Reader.ReadAllAsync(ct))
            yield return line;
    }

    public void Kill()
    {
        try
        {
            // A árvore inteira: o jogo arranca com um bootstrap que gera o
            // processo real, e matar só o pai deixaria o Minecraft a correr sem
            // ninguém a olhar por ele.
            if (!_processo.HasExited)
                _processo.Kill(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            // Já tinha saído entre o teste e o pedido. Não é erro: o objetivo
            // era exatamente este.
        }
    }

    public void Dispose()
    {
        _processo.OutputDataReceived -= AoReceber;
        _processo.ErrorDataReceived -= AoReceber;
        _processo.Dispose();
    }

    private void AoReceber(object sender, DataReceivedEventArgs e)
    {
        // Nulo é o fim do fluxo daquele canal, não uma linha vazia do jogo.
        if (e.Data is not null)
            _linhas.Writer.TryWrite(e.Data);
    }

    private async Task FecharAoSairAsync()
    {
        try
        {
            await _processo.WaitForExitAsync(CancellationToken.None);

            // Sem argumento, o WaitForExitAsync volta assim que o processo morre,
            // mas as callbacks de saída ainda podem estar em voo. O WaitForExit
            // síncrono a seguir é o que garante que elas foram todas entregues —
            // é o par documentado, e sem ele as últimas linhas (as do crash)
            // perdem-se.
            _processo.WaitForExit();
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            // Processo já descartado. O canal fecha na mesma, abaixo.
        }

        _linhas.Writer.TryComplete();
    }
}
