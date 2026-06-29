using System.Reactive;
using ReactiveUI;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Shell do launcher: faz o "gate" entre login e conteúdo. No arranque tenta restaurar a sessão; se
/// houver, mostra o catálogo de modpacks, senão a tela de login. Orquestra os VMs filhos (não duplica
/// a lógica deles).
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly ApiClient _api;

    private object? _currentPage;
    private string? _playerName;
    private bool _busy;

    public MainWindowViewModel(AuthService auth, ApiClient api)
    {
        _auth = auth;
        _api = api;

        Logout = ReactiveCommand.CreateFromTask(LogoutAsync);

        // Restaura a sessão em background; a UI mostra o estado "ocupado" enquanto isso.
        _ = InitializeAsync();
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public string? PlayerName
    {
        get => _playerName;
        private set => this.RaiseAndSetIfChanged(ref _playerName, value);
    }

    public bool IsLoggedIn => PlayerName is not null;

    /// <summary>Inicial do jogador para o avatar (1ª letra, maiúscula).</summary>
    public string? PlayerInitial => PlayerName is { Length: > 0 } n ? n[..1].ToUpperInvariant() : null;

    public bool Busy
    {
        get => _busy;
        private set => this.RaiseAndSetIfChanged(ref _busy, value);
    }

    public ReactiveCommand<Unit, Unit> Logout { get; }

    private async Task InitializeAsync()
    {
        Busy = true;
        try
        {
            // Login silencioso (token do MSAL em cache); null = não autenticado → tela de login.
            var session = await _auth.TryLoginSilentAsync();
            if (session is not null) ShowModpacks(PlayerSession.From(session));
            else ShowLogin();
        }
        finally
        {
            Busy = false;
        }
    }

    private void ShowLogin()
    {
        SetPlayer(null);
        CurrentPage = new LoginViewModel(_auth, OnLoggedIn);
    }

    private void ShowModpacks(PlayerSession session)
    {
        SetPlayer(session.Username);
        CurrentPage = new ModpacksPageViewModel(_api);
    }

    private void OnLoggedIn(PlayerSession session) => ShowModpacks(session);

    private async Task LogoutAsync()
    {
        await _auth.SignOutAsync();
        ShowLogin();
    }

    private void SetPlayer(string? name)
    {
        PlayerName = name;
        this.RaisePropertyChanged(nameof(IsLoggedIn));
        this.RaisePropertyChanged(nameof(PlayerInitial));
    }
}
