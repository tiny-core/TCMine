using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Página "Jogar": hero da instância ativa + botão grande (na shell) + painel de perfil/ID/servidores.
/// O estado de launch e o perfil vivem na shell (<see cref="MainWindowViewModel"/>), exposta via
/// <see cref="Shell"/> para binding direto.
/// </summary>
public sealed class HomePageViewModel : ViewModelBase
{
    private readonly IServerPinger _pinger;

    public HomePageViewModel(MainWindowViewModel shell, IServerPinger pinger)
    {
        Shell = shell;
        _pinger = pinger;
        SelectInstance = ReactiveCommand.Create<InstalledModpack>(shell.SelectActive);
        ToggleAutoJoin = ReactiveCommand.Create<ServerStatusItem>(OnToggleAutoJoin);
        RebuildServers();
        _ = ServerLoopAsync();
    }

    public MainWindowViewModel Shell { get; }

    public ReactiveCommand<InstalledModpack, Unit> SelectInstance { get; }

    /// <summary>Marca/desmarca o servidor de entrada automática (radio: só um ativo).</summary>
    public ReactiveCommand<ServerStatusItem, Unit> ToggleAutoJoin { get; }

    public ObservableCollection<ServerStatusItem> Servers { get; } = [];

    public bool HasServers => Servers.Count > 0;

    public void NotifyActiveChanged() => RebuildServers();

    private void RebuildServers()
    {
        Servers.Clear();
        if (Shell.Active is { } active)
            foreach (var server in active.Servers)
                Servers.Add(new ServerStatusItem(server)
                {
                    IsAutoJoin = server.Name == active.AutoJoinServerName
                });

        this.RaisePropertyChanged(nameof(HasServers));
        _ = RefreshServersAsync();
    }

    /// <summary>
    /// Comportamento "rádio": só um servidor de auto-join. Clicar no que já está ativo desliga (passa a
    /// abrir no menu principal). Persiste no modpack ativo; o JOGAR usa esta marcação.
    /// </summary>
    private void OnToggleAutoJoin(ServerStatusItem item)
    {
        if (Shell.Active is not { } active) return;
        var turnOn = !item.IsAutoJoin;
        foreach (var s in Servers) s.IsAutoJoin = false;
        item.IsAutoJoin = turnOn;
        active.AutoJoinServerName = turnOn ? item.Name : null;
        Shell.SaveInstance(active);
    }

    private async Task ServerLoopAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            await RefreshServersAsync();
        }
    }

    private async Task RefreshServersAsync()
    {
        foreach (var item in Servers.ToList())
        {
            var status = await _pinger.PingAsync(item.Server.Address, item.Server.Port);
            if (!Servers.Contains(item)) continue;

            item.Online = status.Online;
            item.StatusText = status.Online ? $"Online · {status.PlayersOnline}/{status.PlayersMax}" : "Offline";
        }
    }
}

/// <summary>Estado de um servidor do modpack (linha na página Jogar).</summary>
public sealed class ServerStatusItem(ModpackServer server) : ViewModelBase
{
    private bool _online;
    private bool _isAutoJoin;
    private string _statusText = "A verificar…";

    public ModpackServer Server { get; } = server;
    public string Name => Server.Name;

    /// <summary>Este servidor é o de entrada automática ao iniciar.</summary>
    public bool IsAutoJoin
    {
        get => _isAutoJoin;
        set => this.RaiseAndSetIfChanged(ref _isAutoJoin, value);
    }

    public bool Online
    {
        get => _online;
        set => this.RaiseAndSetIfChanged(ref _online, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }
}
