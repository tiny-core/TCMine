using System.Reactive;
using System.Reflection;
using Avalonia.Threading;
using ReactiveUI;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;

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
/// ViewModel raiz (shell). Estado de autenticação + navegação; cria as páginas. Depende só das
/// <b>portas</b> (TCMine-Application) — as implementações vêm da infraestrutura, via composição (Splat).
/// A parte de instâncias/launch está em <c>MainWindowViewModel.Play.cs</c>.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IAuthService _auth;
    private readonly IModpackCatalog _catalog;
    private readonly IContentWatcher _contentWatcher;
    private readonly IUpdateService _updateService;

    private object? _currentPage;
    private AppTab _selectedTab = AppTab.Modpacks;
    private bool _isInitializing = true;
    private bool _isLoggedIn;
    private bool _isAuthenticating;
    private string? _loginError;
    private PlayerSession? _player;
    private ServerStatus _serverStatus = ServerStatus.Checking;
    private string? _updateVersion;
    private bool _isUpdating;
    private string _updateStatus = "";

    public MainWindowViewModel(
        IAuthService auth, IModpackCatalog catalog, IInstanceStore instanceStore, ISettingsStore settingsStore,
        IGameRunStateStore runState, ILaunchOrchestrator orchestrator, IServerPinger pinger, ISystemInfo systemInfo,
        IContentWatcher contentWatcher, INewsFeed newsFeed, IUpdateService updateService)
    {
        _auth = auth;
        _catalog = catalog;
        _instanceStore = instanceStore;
        _settingsStore = settingsStore;
        _runState = runState;
        _orchestrator = orchestrator;
        _systemInfo = systemInfo;
        _contentWatcher = contentWatcher;
        _updateService = updateService;

        InitPlay(); // instâncias instaladas + definições + estado de launch (partial .Play.cs)

        Home = new HomePageViewModel(this, pinger);
        Instances = new InstancesPageViewModel(this);
        Modpacks = new ModpacksPageViewModel(this, catalog);
        News = new NewsPageViewModel(newsFeed);
        Settings = new SettingsPageViewModel(this, systemInfo);

        // Aterragem: Home se já há um modpack ativo (definido em InitPlay/LoadInstalled), senão Modpacks.
        // Mantém o destaque da sidebar coerente com a página mostrada.
        _selectedTab = Active is not null ? AppTab.Home : AppTab.Modpacks;
        _currentPage = _selectedTab == AppTab.Home ? Home : Modpacks;

        LoginMicrosoft = ReactiveCommand.CreateFromTask(LoginAsync,
            this.WhenAnyValue(x => x.IsAuthenticating, busy => !busy));
        Logout = ReactiveCommand.CreateFromTask(LogoutAsync);
        Navigate = ReactiveCommand.Create<AppTab>(tab => SelectedTab = tab);
        UpdateNow = ReactiveCommand.CreateFromTask(ApplyUpdateAsync,
            this.WhenAnyValue(x => x.IsUpdating, busy => !busy));

        // Live link: o servidor avisa (SSE) quando o conteúdo muda → recarrega catálogo + ativo.
        _contentWatcher.ContentChanged += OnServerContentChanged;
        _contentWatcher.ConnectionChanged += OnServerConnectionChanged;
        _contentWatcher.Start();

        _ = InitializeAsync();
    }

    // ── Páginas ──────────────────────────────────────────────────────────────────────────────────
    public HomePageViewModel Home { get; }
    public InstancesPageViewModel Instances { get; }
    public ModpacksPageViewModel Modpacks { get; }
    public NewsPageViewModel News { get; }
    public SettingsPageViewModel Settings { get; }

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

    // ── Autenticação / perfil ────────────────────────────────────────────────────────────────────
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

    public string PlayerName => _player?.Username ?? "";
    public string AvatarInitials => _player?.Initials ?? "?";
    public string? PlayerHeadUrl => _player?.HeadUrl;
    public string AccountLabel => _player?.AccountLabel ?? "";

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

    public bool IsServerOnline => Server == ServerStatus.Online;
    public bool IsServerChecking => Server == ServerStatus.Checking;
    public bool IsServerOffline => Server == ServerStatus.Offline;

    public string VersionLabel => "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    // ── Auto-update (Velopack contra o feed /updates do servidor) ─────────────────────────────────
    public ReactiveCommand<Unit, Unit> UpdateNow { get; }

    /// <summary>Versão nova disponível (null = nenhuma / não instalado / dev).</summary>
    public string? UpdateVersion
    {
        get => _updateVersion;
        private set
        {
            this.RaiseAndSetIfChanged(ref _updateVersion, value);
            this.RaisePropertyChanged(nameof(IsUpdateAvailable));
        }
    }

    public bool IsUpdateAvailable => _updateVersion is not null;

    public bool IsUpdating
    {
        get => _isUpdating;
        private set => this.RaiseAndSetIfChanged(ref _isUpdating, value);
    }

    /// <summary>Texto do progresso do update (ex.: "Baixando 42%").</summary>
    public string UpdateStatus
    {
        get => _updateStatus;
        private set => this.RaiseAndSetIfChanged(ref _updateStatus, value);
    }

    // ── Fluxo ────────────────────────────────────────────────────────────────────────────────────
    private async Task InitializeAsync()
    {
        _ = CheckServerAsync();
        _ = RefreshActiveAsync(); // atualiza metadados do modpack ativo (incl. servidores) do manifesto
        _ = ReconcileAvailabilityAsync(); // marca instâncias cujo modpack já não existe no servidor
        _ = CheckUpdateAsync(); // há uma versão nova do launcher no feed do servidor?
        try
        {
            var session = await _auth.TryLoginSilentAsync();
            if (session is not null) SetLoggedIn(session);
        }
        finally
        {
            IsInitializing = false;
        }
    }

    // Verifica o feed /updates; se houver versão nova (e a app estiver instalada), mostra o aviso
    private async Task CheckUpdateAsync()
    {
        try
        {
            UpdateVersion = await _updateService.CheckAsync();
        }
        catch
        {
            // sem rede / feed indisponível — sem aviso, sem ruído
        }
    }

    // Baixa e aplica o update; a app reinicia sozinha ao aplicar (não retorna daqui em caso de sucesso)
    private async Task ApplyUpdateAsync()
    {
        IsUpdating = true;
        UpdateStatus = "Iniciando…";
        try
        {
            var progress = new Progress<int>(p => UpdateStatus = $"Baixando {p}%");
            await _updateService.DownloadAndApplyAsync(progress);
            // Se chegou aqui sem reiniciar, algo impediu (ex.: não instalado)
            UpdateStatus = "Nada para aplicar.";
        }
        catch (Exception ex)
        {
            UpdateStatus = "Falha ao atualizar: " + ex.Message;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private async Task CheckServerAsync()
    {
        Server = ServerStatus.Checking;
        Server = await _catalog.PingAsync() ? ServerStatus.Online : ServerStatus.Offline;
    }

    /// <summary>O servidor avisou que o conteúdo mudou — recarrega o catálogo e o modpack ativo.</summary>
    private void OnServerContentChanged() => Dispatcher.UIThread.Post(() =>
    {
        Modpacks.Reload();
        News.Reload();
        _ = RefreshActiveAsync();          // servidores + metadados do ativo (via manifesto)
        _ = ReconcileAvailabilityAsync();  // badges de "modpack removido" em toda a lista de instâncias
    });

    /// <summary>A ligação SSE ligou/desligou — atualiza o indicador da barra de estado.</summary>
    private void OnServerConnectionChanged(bool connected) => Dispatcher.UIThread.Post(() =>
        Server = connected ? ServerStatus.Online : ServerStatus.Offline);

    private async Task LoginAsync()
    {
        LoginError = null;
        IsAuthenticating = true;
        try
        {
            SetLoggedIn(await _auth.LoginAsync());
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
        SetPlayer(null);
        IsLoggedIn = false;
        SelectedTab = AppTab.Modpacks;
    }

    private void SetLoggedIn(PlayerSession session)
    {
        SetPlayer(session);
        IsLoggedIn = true;
    }

    private void SetPlayer(PlayerSession? session)
    {
        _player = session;
        this.RaisePropertyChanged(nameof(PlayerName));
        this.RaisePropertyChanged(nameof(AvatarInitials));
        this.RaisePropertyChanged(nameof(PlayerHeadUrl));
        this.RaisePropertyChanged(nameof(AccountLabel));
    }
}
