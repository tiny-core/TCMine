using System.Reactive;
using System.Reflection;
using ReactiveUI;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>Abas de navegação da aplicação (sidebar).</summary>
public enum AppTab
{
    Home,
    Instances,
    Modpacks,
    News,
    Settings
}

/// <summary>Estado da ligação ao servidor (indicador na barra de estado).</summary>
public enum ServerStatus
{
    Checking,
    Online,
    Offline
}

/// <summary>
/// ViewModel raiz (shell). Mantém o estado de autenticação e navegação, cria as páginas e expõe os
/// comandos do login (MSAL) e da sidebar. A tela de login é renderizada pela própria `LoginView`
/// ligada a este VM (como no backup); as páginas trocam via `CurrentPage`.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly ApiClient _api;

    private object? _currentPage;
    private AppTab _selectedTab = AppTab.Modpacks;
    private bool _isInitializing = true;
    private bool _isLoggedIn;
    private bool _isAuthenticating;
    private string? _loginError;
    private string _playerName = "";
    private ServerStatus _serverStatus = ServerStatus.Checking;

    public MainWindowViewModel(AuthService auth, ApiClient api)
    {
        _auth = auth;
        _api = api;

        // Páginas criadas uma vez. Modpacks é real; as demais são placeholders até as features chegarem.
        Home = new PlaceholderPageViewModel("Jogar", "A página de jogo chega quando o lançamento estiver pronto.", "IconPlay");
        Instances = new PlaceholderPageViewModel("Instâncias", "Em breve: gerir as instâncias instaladas.", "IconInstances");
        Modpacks = new ModpacksPageViewModel(api);
        News = new PlaceholderPageViewModel("Novidades", "Em breve: novidades do servidor.", "IconNews");
        Settings = new PlaceholderPageViewModel("Definições", "Em breve: preferências do launcher.", "IconSettings");
        _currentPage = Modpacks;

        LoginMicrosoft = ReactiveCommand.CreateFromTask(LoginAsync,
            this.WhenAnyValue(x => x.IsAuthenticating, busy => !busy));
        Logout = ReactiveCommand.CreateFromTask(LogoutAsync);
        Navigate = ReactiveCommand.Create<AppTab>(tab => SelectedTab = tab);

        _ = InitializeAsync();
    }

    // ── Páginas ──────────────────────────────────────────────────────────────────────────────────
    public PlaceholderPageViewModel Home { get; }
    public PlaceholderPageViewModel Instances { get; }
    public ModpacksPageViewModel Modpacks { get; }
    public PlaceholderPageViewModel News { get; }
    public PlaceholderPageViewModel Settings { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    // ── Navegação ────────────────────────────────────────────────────────────────────────────────
    public AppTab SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (_selectedTab == value) return;
            this.RaiseAndSetIfChanged(ref _selectedTab, value);
            this.RaisePropertyChanged(nameof(IsHomeSelected));
            this.RaisePropertyChanged(nameof(IsInstancesSelected));
            this.RaisePropertyChanged(nameof(IsModpacksSelected));
            this.RaisePropertyChanged(nameof(IsNewsSelected));
            this.RaisePropertyChanged(nameof(IsSettingsSelected));
            CurrentPage = value switch
            {
                AppTab.Instances => Instances,
                AppTab.Modpacks => Modpacks,
                AppTab.News => News,
                AppTab.Settings => Settings,
                _ => Home
            };
        }
    }

    public bool IsHomeSelected => SelectedTab == AppTab.Home;
    public bool IsInstancesSelected => SelectedTab == AppTab.Instances;
    public bool IsModpacksSelected => SelectedTab == AppTab.Modpacks;
    public bool IsNewsSelected => SelectedTab == AppTab.News;
    public bool IsSettingsSelected => SelectedTab == AppTab.Settings;

    public ReactiveCommand<AppTab, Unit> Navigate { get; }

    // ── Autenticação ─────────────────────────────────────────────────────────────────────────────
    public bool IsInitializing
    {
        get => _isInitializing;
        private set => this.RaiseAndSetIfChanged(ref _isInitializing, value);
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        private set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value);
    }

    public bool IsAuthenticating
    {
        get => _isAuthenticating;
        private set => this.RaiseAndSetIfChanged(ref _isAuthenticating, value);
    }

    public string? LoginError
    {
        get => _loginError;
        private set => this.RaiseAndSetIfChanged(ref _loginError, value);
    }

    public string PlayerName
    {
        get => _playerName;
        private set => this.RaiseAndSetIfChanged(ref _playerName, value);
    }

    /// <summary>Iniciais do jogador para o avatar (1–2 letras, maiúsculas).</summary>
    public string AvatarInitials
    {
        get
        {
            var parts = PlayerName.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[1][..1]).ToUpperInvariant()
            };
        }
    }

    public ReactiveCommand<Unit, Unit> LoginMicrosoft { get; }
    public ReactiveCommand<Unit, Unit> Logout { get; }

    // ── Estado do servidor (barra de estado) ─────────────────────────────────────────────────────
    public ServerStatus Server
    {
        get => _serverStatus;
        private set
        {
            this.RaiseAndSetIfChanged(ref _serverStatus, value);
            this.RaisePropertyChanged(nameof(ServerStatusLabel));
            this.RaisePropertyChanged(nameof(IsServerOnline));
            this.RaisePropertyChanged(nameof(IsServerChecking));
            this.RaisePropertyChanged(nameof(IsServerOffline));
        }
    }

    public string ServerStatusLabel => Server switch
    {
        ServerStatus.Online => "Servidor ligado",
        ServerStatus.Checking => "A ligar ao servidor…",
        _ => "Servidor indisponível"
    };

    // Um ponto colorido por estado (cor via DynamicResource na View, sem hex no VM).
    public bool IsServerOnline => Server == ServerStatus.Online;
    public bool IsServerChecking => Server == ServerStatus.Checking;
    public bool IsServerOffline => Server == ServerStatus.Offline;

    /// <summary>Rótulo de versão do launcher (barra de estado).</summary>
    public string VersionLabel => "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    // ── Fluxo ────────────────────────────────────────────────────────────────────────────────────
    private async Task InitializeAsync()
    {
        _ = CheckServerAsync();
        try
        {
            var session = await _auth.TryLoginSilentAsync();
            if (session is not null) SetLoggedIn(PlayerSession.From(session));
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private async Task CheckServerAsync()
    {
        Server = ServerStatus.Checking;
        Server = await _api.PingAsync() ? ServerStatus.Online : ServerStatus.Offline;
    }

    private async Task LoginAsync()
    {
        LoginError = null;
        IsAuthenticating = true;
        try
        {
            var session = await _auth.LoginAsync();
            SetLoggedIn(PlayerSession.From(session));
        }
        catch (Exception ex)
        {
            LoginError = ex.Message;
        }
        finally
        {
            IsAuthenticating = false;
        }
    }

    private async Task LogoutAsync()
    {
        await _auth.SignOutAsync();
        PlayerName = "";
        this.RaisePropertyChanged(nameof(AvatarInitials));
        IsLoggedIn = false;
        SelectedTab = AppTab.Modpacks;
    }

    private void SetLoggedIn(PlayerSession session)
    {
        PlayerName = session.Username;
        this.RaisePropertyChanged(nameof(AvatarInitials));
        IsLoggedIn = true;
    }
}
