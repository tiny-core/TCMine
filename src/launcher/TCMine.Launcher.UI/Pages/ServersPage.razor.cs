using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.UI.Components;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

/// <summary>
///     Todos os servidores que a conta vê, abertos e com convite, cada um com a
///     ação que cabe: pedir acesso, instalar, jogar, ou atualizar e jogar.
///     A pasta de cada servidor é a instância DELE (ver
///     <see cref="PrepareServerInstance" />), nunca uma pessoal do mesmo pack.
/// </summary>
public partial class ServersPage : ComponentBase, IDisposable
{
    /// <summary>Instância de cada servidor que já tem pasta aqui, pelo id do servidor.</summary>
    private Dictionary<Guid, InstanceManifest> _instances = [];

    private double? _fraction;
    private Guid? _joining;
    private string? _message;
    private Severity _messageSeverity = Severity.Warning;
    private string? _phase;

    [Inject] private CatalogStore Catalog { get; set; } = default!;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private PrepareServerInstance Prepare { get; set; } = default!;

    [Inject] private JoinServer Join { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private InstallOperationState Operation { get; set; } = default!;

    /// <summary>Jogo aberto ou instalação em curso: as ações daqui ficam desligadas.</summary>
    [Inject]
    private ActionLock Lock { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

    /// <summary>
    ///     Cada servidor com o pack a que pertence, os que já dão para entrar
    ///     primeiro. Calculado a cada desenho a partir do catálogo guardado, como
    ///     na tela inicial: uma cópia num campo era o que ficava velha quando o
    ///     catálogo mudava com a tela aberta.
    /// </summary>
    private IEnumerable<(ModpackDto Modpack, GameServerDto Server)> Rows =>
        (Catalog.Current?.Entries ?? [])
        .SelectMany(e => e.Servers.Select(s => (e.Modpack, Server: s)))
        .OrderBy(r => r.Server.AccessState is ServerAccessState.Granted ? 0 : 1)
        .ThenBy(r => r.Server.Name, StringComparer.CurrentCultureIgnoreCase);

    private CatalogView? Shown => Catalog.View;

    public void Dispose()
    {
        Operation.Changed -= OnChanged;
        Lock.Changed -= OnChanged;
        Catalog.Changed -= OnChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // Assina ANTES de carregar, pelo mesmo motivo do catálogo: uma
        // instalação que termina no meio do caminho não pode ser perdida.
        Operation.Changed += OnChanged;
        Lock.Changed += OnChanged;
        Catalog.Changed += OnChanged;

        await Catalog.EnsureLoadedAsync();
        await RefreshInstancesAsync();
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private Task LoadAsync() => Catalog.RefreshAsync();

    private ServerEntryAction ActionFor(GameServerDto server) =>
        ServerEntry.Of(server, _instances.GetValueOrDefault(server.Id));

    /// <summary>
    ///     Por que o botão principal está desligado, ou nulo. Instalar não
    ///     depende do servidor estar no ar — deixar a pasta pronta é justamente o
    ///     que poupa a espera quando ele voltar.
    /// </summary>
    private string? WhyDisabled(GameServerDto server) =>
        Lock.Reason
        ?? (_joining is not null ? "Abrindo o jogo…"
            : Shell.Pairing?.Config is null ? "Pareie com um servidor para poder jogar."
            : ActionFor(server) is ServerEntryAction.Install ? null
            : ServerEntry.Unavailable(server));

    private async Task RefreshInstancesAsync()
    {
        // Só os manifestos: a tela não mostra tamanho, e somar pastas com
        // mundo dentro custaria segundos por abertura.
        var manifests = await Instances.HandleLightAsync(CancellationToken.None);
        var byServer = new Dictionary<Guid, InstanceManifest>();

        foreach (var manifest in manifests)
        {
            if (manifest.ServerId is { } serverId)
                byServer.TryAdd(serverId, manifest);
        }

        _instances = byServer;
    }

    /// <summary>
    ///     Instala a pasta do servidor sem abrir o jogo. Pela mesma trava das
    ///     outras instalações: duas ao mesmo tempo disputariam o armazém e a
    ///     banda, e o jogador veria duas barras a meia velocidade.
    /// </summary>
    private async Task InstallAsync(ModpackDto modpack, GameServerDto server)
    {
        if (Shell.Pairing?.Config is not { } config || Lock.IsLocked)
            return;

        _message = null;
        Operation.Begin(server.Id);

        try
        {
            var result = await Prepare.HandleAsync(
                config.ServerUrl, modpack, server, new Progress<InstallProgress>(Operation.Report),
                CancellationToken.None);

            if (!result.Succeeded)
                Show(result.Error, Severity.Error);
            else if (result.Warning is { } warning)
                Show(warning, Severity.Warning);

            await RefreshInstancesAsync();
        }
        finally
        {
            Operation.Finish();
        }
    }

    /// <summary>
    ///     Entra no servidor: instala ou alinha a pasta dele, se preciso, e abre
    ///     o jogo. A trava de instalação fica presa durante a preparação,
    ///     senão o catálogo deixaria começar outra por baixo; com o jogo aberto,
    ///     quem segura é a sessão do jogo.
    /// </summary>
    private async Task PlayAsync(ModpackDto modpack, GameServerDto server)
    {
        if (Shell.Pairing?.Config is not { } config || Lock.IsLocked || _joining is not null)
            return;

        _joining = server.Id;
        _message = null;
        Operation.Begin(server.Id);

        var progress = new Progress<GameLaunchProgress>(p =>
        {
            _phase = p.Phase;
            _fraction = p.Fraction;
            InvokeAsync(StateHasChanged);
        });

        try
        {
            var result = await Join.HandleAsync(modpack, server, config, progress, CancellationToken.None);

            // O erro fica NA TELA e não num snackbar: preparar a pasta pode
            // demorar minutos, e uma mensagem que some sozinha some antes de o
            // jogador voltar a olhar.
            if (!result.Launch.Started)
                Show(result.Launch.Message, Severity.Warning);
            else if (result.Warning is { } warning)
                Show(warning, Severity.Warning);

            await RefreshInstancesAsync();
        }
        finally
        {
            Operation.Finish();
            _joining = null;
            _phase = null;
            _fraction = null;
        }
    }

    private void Show(string? message, Severity severity)
    {
        _message = message;
        _messageSeverity = severity;
    }

    private async Task RedeemInviteAsync()
    {
        var dialog = await Dialogs.ShowAsync<RedeemInviteDialog>("Resgatar convite");
        var result = await dialog.Result;

        // Recarrega para o servidor recém-liberado aparecer sem reabrir a tela.
        if (result is { Canceled: false })
            await Catalog.RefreshAsync();
    }
}
