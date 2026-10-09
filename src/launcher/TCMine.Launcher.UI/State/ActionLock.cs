using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     Se as ações do launcher estão travadas agora, e porquê.
///     Duas coisas travam: o jogo aberto e uma instalação em curso. Com o jogo
///     aberto, atualizar, remover ou trocar a RAM de uma instância mexe em
///     arquivos que o Java está a ler — e o mundo é escrito a cada autosave —,
///     instalar outro pack disputa disco e banda com o jogo, e sair da conta
///     deixaria o jogo aberto com uma sessão que já não existe.
///     Um lugar só para a regra: cada tela perguntava ao seu jeito (a de
///     instâncias olhava a instalação, a de jogar olhava o jogo), e um botão
///     esquecido em qualquer uma delas era um botão que funcionava quando não
///     devia.
///     Singleton como as duas fontes. Avisa só quando a TRAVA muda: o
///     <see cref="GameSession.Changed" /> dispara a cada linha de log — milhares —
///     e repassá-lo faria cada tela redesenhar a cada linha.
/// </summary>
public sealed class ActionLock : IDisposable
{
    private readonly GameSession _game;
    private readonly InstallOperationState _operation;
    private string? _lastReason;

    public ActionLock(InstallOperationState operation, GameSession game)
    {
        _operation = operation;
        _game = game;
        _lastReason = Reason;

        _operation.Changed += OnSourceChanged;
        _game.Changed += OnSourceChanged;
    }

    public bool IsLocked => Reason is not null;

    /// <summary>O motivo para mostrar no botão desligado, ou nulo quando está tudo livre.</summary>
    public string? Reason =>
        _game.IsRunning ? "Feche o jogo para usar isto."
        : _operation.IsRunning ? "Espere a instalação em curso terminar."
        : null;

    public void Dispose()
    {
        _operation.Changed -= OnSourceChanged;
        _game.Changed -= OnSourceChanged;
    }

    public event Action? Changed;

    private void OnSourceChanged()
    {
        var reason = Reason;
        if (reason == _lastReason)
            return;

        _lastReason = reason;
        Changed?.Invoke();
    }
}
