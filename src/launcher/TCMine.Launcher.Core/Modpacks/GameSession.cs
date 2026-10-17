using System.Collections.Concurrent;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     O jogo que está a correr, e o que ele escreveu.
///     Singleton, e no Core: sobrevive à navegação entre telas — o jogador abre o
///     jogo, vai ver a lista de modpacks e volta, e tem de continuar a ver que
///     está a jogar. É o mesmo motivo do <c>LauncherShellState</c>, mas isto tem
///     regras que merecem teste, e por isso mora deste lado.
///     A que mais importa: enquanto há jogo aberto, não se abre outro. Duas
///     cópias na mesma pasta escrevem o mesmo mundo ao mesmo tempo e corrompem-no
///     — e o jogador que clica duas vezes não faz ideia de que foi isso.
///     O jogo aberto também fica gravado no disco (<see cref="IRunningGameStore" />)
///     enquanto corre: é o que deixa um launcher reaberto reencontrá-lo.
/// </summary>
/// <param name="records">Nulo nos testes que não se importam com o disco.</param>
public sealed class GameSession(IRunningGameStore? records = null)
{
    /// <summary>
    ///     Quantas linhas guardar. Um modpack grande escreve dezenas de milhares
    ///     no arranque; guardar tudo seria centenas de megabytes em memória por
    ///     uma tela que mostra as últimas. As antigas caem pela frente, que é o
    ///     lado certo: o que interessa num crash é o fim.
    /// </summary>
    public const int MaxLogLines = 1000;

    private readonly ConcurrentQueue<string> _log = new();
    private readonly Lock _porta = new();

    private IGameProcess? _processo;

    /// <summary>
    ///     As gravações do registo em fila, uma de cada vez: um jogo que fecha
    ///     logo depois de abrir não pode ter o "apagar" a correr antes do
    ///     "gravar", senão o disco ficaria a dizer que há jogo aberto.
    /// </summary>
    private Task _recording = Task.CompletedTask;

    /// <summary>A instância a correr, ou nulo quando não há jogo aberto.</summary>
    public InstalledInstance? Running { get; private set; }

    public bool IsRunning => Running is not null;

    /// <summary>
    ///     O jogo foi reencontrado ao reabrir o launcher, e não aberto por ele.
    ///     A janela não minimiza nesse caso: quem acabou de abrir o launcher quer
    ///     vê-lo.
    /// </summary>
    public bool IsReattached { get; private set; }

    /// <summary>
    ///     A ligação ao servidor passa pelo túnel deste launcher. Fechá-lo
    ///     derrubaria o jogo do servidor — é o que decide se sair avisa.
    /// </summary>
    public bool UsesTunnel
    {
        get
        {
            lock (_porta) return _processo is TunneledGameProcess;
        }
    }

    /// <summary>
    ///     A instância do último jogo aberto, e continua depois de ele fechar:
    ///     é depois do crash que alguém vai procurar a pasta de logs dele, e a
    ///     janela do console tem de saber qual é.
    /// </summary>
    public InstalledInstance? LastInstance { get; private set; }

    /// <summary>
    ///     Código de saída da ÚLTIMA execução, quando ela já terminou.
    ///     Fica disponível depois de o jogo fechar de propósito: um código
    ///     diferente de zero é a única pista de que o jogo não fechou sozinho, e
    ///     apagá-lo no fecho tiraria a informação exatamente quando ela nasce.
    /// </summary>
    public int? LastExitCode { get; private set; }

    public IReadOnlyList<string> Log => [.. _log];

    public event Action? Changed;

    /// <summary>
    ///     Toma conta do processo e começa a bombear o log.
    ///     Devolve falso se já havia jogo aberto — quem chama tem de fechar o
    ///     processo novo, porque nesse caso ele não é de ninguém.
    /// </summary>
    public bool Attach(InstalledInstance instance, IGameProcess process) => Take(instance, process, false);

    /// <summary>
    ///     Toma conta de um jogo que outro launcher abriu (ver
    ///     <see cref="ResumeRunningGame" />). O registo no disco já é o dele, e
    ///     fica.
    /// </summary>
    public bool Reattach(InstalledInstance instance, IGameProcess process) => Take(instance, process, true);

    private bool Take(InstalledInstance instance, IGameProcess process, bool reattached)
    {
        lock (_porta)
        {
            if (_processo is not null)
                return false;

            _processo = process;
            Running = instance;
            LastInstance = instance;
            LastExitCode = null;
            IsReattached = reattached;
        }

        _log.Clear();
        Changed?.Invoke();

        // Gravado ANTES de a bomba arrancar: um jogo que fecha logo a seguir
        // enfileira o "apagar" depois deste "gravar", e o disco não fica a dizer
        // que há jogo aberto.
        if (!reattached && process.Identity is { } identity)
            Record(store => store.WriteAsync(new RunningGameRecord(instance.Key, identity), CancellationToken.None));

        // Sem await de propósito: a bomba vive enquanto o jogo viver, e quem
        // abriu o jogo não pode ficar preso até ele fechar.
        _ = PumpAsync(process);

        return true;
    }

    /// <summary>Mata o jogo. Para quando ele pendura e não há mais saída.</summary>
    public void Kill()
    {
        lock (_porta) _processo?.Kill();
    }

    private async Task PumpAsync(IGameProcess process)
    {
        try
        {
            await foreach (var line in process.ReadOutputAsync(CancellationToken.None))
            {
                _log.Enqueue(line);

                while (_log.Count > MaxLogLines)
                    _log.TryDequeue(out _);

                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            // Ler o log nunca pode derrubar o launcher: o jogo continua a correr
            // e o jogador não deve perder a aplicação por causa de um pipe.
            _log.Enqueue($"[launcher] A leitura do log terminou: {ex.Message}");
        }
        finally
        {
            lock (_porta)
            {
                LastExitCode = ExitCodeOrNull(process);
                _processo = null;
                Running = null;
            }

            process.Dispose();
            Record(store => store.ClearAsync(CancellationToken.None));
            Changed?.Invoke();
        }
    }

    /// <summary>
    ///     Grava no disco sem prender quem chama, e sem deixar uma falha subir:
    ///     o registo é conveniência (reencontrar o jogo), e um disco cheio não
    ///     pode impedir de jogar.
    /// </summary>
    private void Record(Func<IRunningGameStore, Task> write)
    {
        if (records is null)
            return;

        lock (_porta)
            _recording = Chain(_recording, write, records);
    }

    private static async Task Chain(Task previous, Func<IRunningGameStore, Task> write, IRunningGameStore store)
    {
        await previous;

        try
        {
            await write(store);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ver Record: sem registo, só não se reencontra o jogo.
        }
    }

    /// <summary>
    ///     O código de saída, se der para o saber.
    ///     Um processo morto à força ou já descartado pode não o ter, e não ter
    ///     código é melhor do que rebentar aqui — este método corre no
    ///     <c>finally</c> que limpa o estado, e falhar nele deixaria o launcher a
    ///     achar que ainda há um jogo aberto para sempre.
    /// </summary>
    private static int? ExitCodeOrNull(IGameProcess process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            return null;
        }
    }
}
