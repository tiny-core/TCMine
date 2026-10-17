using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Options;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     Envia as teclas para a conta quando o jogo fecha — o jogado por aqui ou
///     o reencontrado ao reabrir o launcher.
///     Ouve o jogo e não uma tela: o jogo fecha com o jogador noutra página, ou
///     com o launcher na bandeja. Singleton, criado pelo <c>ShellLayout</c>
///     como o <see cref="GameWindowCoordinator" />.
/// </summary>
public sealed class PlayerOptionsCoordinator : IDisposable
{
    private readonly GameSession _game;
    private readonly SyncPlayerOptions _sync;
    private bool _wasRunning;

    public PlayerOptionsCoordinator(GameSession game, SyncPlayerOptions sync)
    {
        _game = game;
        _sync = sync;
        _wasRunning = game.IsRunning;

        _game.Changed += OnGameChanged;
    }

    /// <summary>O envio em curso, para quem precisa de esperar por ele (os testes).</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    public void Dispose() => _game.Changed -= OnGameChanged;

    /// <summary>Só na transição para fechado; o Changed dispara a cada linha de log.</summary>
    private void OnGameChanged()
    {
        var running = _game.IsRunning;

        if (running == _wasRunning)
            return;

        _wasRunning = running;

        // Sem esperar: este aviso chega do fio que lê o jogo, e a rede não
        // pode segurá-lo. O caso de uso nunca lança.
        if (!running && _game.LastInstance is { } instance)
            Pending = _sync.AfterExitAsync(instance, CancellationToken.None);
    }
}
