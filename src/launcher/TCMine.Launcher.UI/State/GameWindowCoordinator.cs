using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     A janela acompanha o jogo: minimiza quando ele abre, volta quando ele
///     fecha.
///     Minimizar tira o launcher do caminho do jogo; voltar é o que mostra na
///     hora um crash ("o jogo fechou com erro") em vez de deixar o jogador a
///     olhar para o ambiente de trabalho sem saber o que houve.
///     Um jogo REENCONTRADO não minimiza: quem acabou de abrir o launcher quer
///     vê-lo.
///     Singleton, e criado pelo <c>ShellLayout</c>: tem de ouvir o jogo desde o
///     arranque, venha a abertura de que tela vier.
/// </summary>
public sealed class GameWindowCoordinator : IDisposable
{
    private readonly IWindowChrome _chrome;
    private readonly GameSession _game;
    private bool _wasRunning;

    public GameWindowCoordinator(GameSession game, IWindowChrome chrome)
    {
        _game = game;
        _chrome = chrome;
        _wasRunning = game.IsRunning;

        _game.Changed += OnGameChanged;
    }

    public void Dispose() => _game.Changed -= OnGameChanged;

    /// <summary>
    ///     Só nas transições: o Changed dispara a cada linha de log, e minimizar
    ///     a janela mil vezes por segundo seria o jeito mais caro de não fazer
    ///     nada.
    /// </summary>
    private void OnGameChanged()
    {
        var running = _game.IsRunning;

        if (running == _wasRunning)
            return;

        _wasRunning = running;

        if (running)
        {
            if (!_game.IsReattached)
                _chrome.Minimize();
        }
        else
        {
            _chrome.Restore();
        }
    }
}
