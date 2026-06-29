using System.Reactive;
using ReactiveUI;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Tela de login: um botão que dispara o fluxo Microsoft (orquestrado pelo servidor) e mostra o
/// progresso enquanto o jogador completa o login no navegador.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly Action<PlayerSession> _onLoggedIn;

    private bool _busy;
    private string? _status;
    private string? _error;

    public LoginViewModel(AuthService auth, Action<PlayerSession> onLoggedIn)
    {
        _auth = auth;
        _onLoggedIn = onLoggedIn;

        // Desabilita o botão enquanto o login está em curso (evita disparos duplicados).
        var canSignIn = this.WhenAnyValue(x => x.Busy, busy => !busy);
        SignIn = ReactiveCommand.CreateFromTask(SignInAsync, canSignIn);
    }

    public bool Busy
    {
        get => _busy;
        private set => this.RaiseAndSetIfChanged(ref _busy, value);
    }

    public string? Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public string? Error
    {
        get => _error;
        private set => this.RaiseAndSetIfChanged(ref _error, value);
    }

    public ReactiveCommand<Unit, Unit> SignIn { get; }

    private async Task SignInAsync()
    {
        Error = null;
        Busy = true;
        Status = "Abrindo o login da Microsoft…";
        try
        {
            var session = await _auth.LoginAsync();
            Status = "Login concluído.";
            _onLoggedIn(PlayerSession.From(session));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = null;
        }
        finally
        {
            Busy = false;
        }
    }
}
