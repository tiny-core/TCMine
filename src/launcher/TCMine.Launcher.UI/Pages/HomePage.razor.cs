using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Components;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class HomePage : ComponentBase, IDisposable
{
    private InstalledInstance? _active;
    private bool _needsChoice;
    private bool _loading = true;
    private IReadOnlyList<GameServerDto> _servers = [];
    private IReadOnlyList<ModpackNewsDto> _news = [];

    private bool _launching;
    private Guid? _joining;
    private string? _phase;
    private double? _fraction;
    private string? _error;

    /// <summary>
    ///     Há conta e pareamento para abrir o jogo.
    ///     Verificado aqui e não dentro do caso de uso porque o botão precisa
    ///     nascer desabilitado: oferecer "Jogar" a quem não entrou faria o clique
    ///     existir só para recusar.
    /// </summary>
    /// <summary>
    ///     Basta haver pareamento — é dele que sai o client id do Azure.
    ///     Já exigiu sessão no servidor TCMine, e isso estava errado: quem o jogo
    ///     precisa de conhecer é a conta Minecraft, e o caso de uso resolve-a
    ///     sozinho (perfil vivo, perfil guardado, ou modo offline). Exigir sessão
    ///     aqui desligava o botão justamente quando o servidor estava fora.
    /// </summary>
    private bool CanPlay => Shell.Pairing?.Config is not null;

    /// <summary>O único caso que resta: nem sequer há servidor pareado.</summary>
    private const string WhyCannotPlay = "Pareie com um servidor para poder jogar.";

    private string? JoinBlockedReason =>
        Game.IsRunning ? "O jogo já está aberto."
        : _launching ? "Abrindo o jogo…"
        : CanPlay ? null
        : WhyCannotPlay;

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private IServerConnection Connection { get; set; } = default!;

    [Inject] private GameSession Game { get; set; } = default!;

    [Inject] private LaunchGame Launch { get; set; } = default!;

    [Inject] private JoinServer Join { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

    /// <summary>
    ///     Deixa de ouvir a sessão ao sair da tela.
    ///     Sem isto cada visita acumula um assinante num singleton que vive tanto
    ///     quanto a aplicação, e o jogo passaria a redesenhar telas mortas — uma
    ///     vez por linha de log, que são milhares.
    /// </summary>
    public void Dispose()
    {
        Game.Changed -= OnGameChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // O jogo pode já estar a correr: o jogador abriu, foi ver os modpacks e
        // voltou. A tela tem de o encontrar assim, e não em branco.
        Game.Changed += OnGameChanged;

        try
        {
            var vista = await Active.CurrentAsync(CancellationToken.None);

            _active = vista.Active;
            _needsChoice = vista.NeedsChoice;
        }
        finally
        {
            _loading = false;
        }

        // Depois de a tela já poder desenhar: o card da instância é o que
        // importa e vem do disco, enquanto os servidores dependem de rede. Sem
        // esta ordem, um servidor lento deixaria a tela de jogar em branco.
        await LoadSideAsync();
    }

    /// <summary>
    ///     Redesenha quando o jogo escreve ou fecha.
    ///     Vem de um fio de fundo, por isso o InvokeAsync: tocar no estado do
    ///     componente fora do circuito é o caminho para uma tela que congela.
    /// </summary>
    private void OnGameChanged() => InvokeAsync(StateHasChanged);

    private Task StopAsync()
    {
        Game.Kill();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Abre o jogo, com o andamento à vista.
    ///     O erro fica NA TELA e não num snackbar: preparar o Java pode demorar
    ///     minutos, e uma mensagem que desaparece sozinha some antes de o jogador
    ///     voltar a olhar — justamente quando ela é a única explicação do que
    ///     aconteceu.
    /// </summary>
    private Task PlayAsync() =>
        RunLaunchAsync((active, config, progress) =>
            Launch.HandleAsync(active, config, progress, CancellationToken.None));

    /// <summary>
    ///     Abre o jogo já dentro do servidor, alinhando antes a versão da
    ///     instância com a dele — o andamento e o erro aparecem no mesmo sítio do
    ///     "Jogar", porque é a mesma abertura.
    /// </summary>
    private async Task JoinAsync(GameServerDto server)
    {
        _joining = server.Id;

        try
        {
            await RunLaunchAsync(async (active, config, progress) =>
            {
                var result = await Join.HandleAsync(active, server, config, progress, CancellationToken.None);

                // Atualizada no caminho: o cabeçalho mostra a versão, e
                // continuar com a antiga em memória faria o "Jogar" seguinte
                // abrir com o manifesto velho.
                if (result.Updated is { } updated)
                    _active = updated;

                return result.Launch;
            });
        }
        finally
        {
            _joining = null;
        }
    }

    private async Task RunLaunchAsync(
        Func<InstalledInstance, LauncherConfig, IProgress<GameLaunchProgress>, Task<GameLaunchResult>> launch)
    {
        if (_active is null || Shell.Pairing?.Config is not { } config || _launching)
            return;

        _launching = true;
        _error = null;
        _fraction = null;

        var progress = new Progress<GameLaunchProgress>(p =>
        {
            _phase = p.Phase;
            _fraction = p.Fraction;

            // Progress<T> chega no contexto do circuito mas fora do ciclo de
            // render: sem isto a barra fica parada até o próximo evento da tela.
            InvokeAsync(StateHasChanged);
        });

        try
        {
            var result = await launch(_active, config, progress);

            if (!result.Started)
                _error = result.Message;
        }
        finally
        {
            _launching = false;
            _phase = null;
            _fraction = null;
        }
    }

    /// <summary>
    ///     A coluna da direita: servidores e novidades do pack ativo.
    ///     Falhar aqui é silencioso de propósito. Esta tela tem de servir
    ///     offline, e um erro vermelho sobre o catálogo faria parecer que a
    ///     instância instalada também está com problema — quando ela está inteira
    ///     no disco, e o botão de jogar continua a funcionar.
    /// </summary>
    private async Task LoadSideAsync()
    {
        if (_active is null || Shell.Pairing?.Config is not { } config)
            return;

        var catalog = await Catalog.HandleAsync(config.ServerUrl, CancellationToken.None);

        if (catalog.Failed)
            return;

        _servers = catalog.Entries
            .FirstOrDefault(e => e.Modpack.Id == _active.Manifest.ModpackId)
            ?.Servers ?? [];

        StateHasChanged();

        // Depois dos servidores, e numa chamada à parte: as novidades são o que
        // menos importa nesta tela, e falhar a buscá-las não pode levar os
        // servidores consigo.
        try
        {
            _news = await Connection.GetNewsAsync(_active.Manifest.ModpackId, CancellationToken.None);
            StateHasChanged();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem novidades a coluna simplesmente não as mostra.
        }
    }

    /// <summary>
    ///     Abre o diálogo de resgate e, se um convite entrar, recarrega a
    ///     coluna lateral — é só assim que o servidor recém-liberado aparece
    ///     sem o jogador precisar reabrir a tela.
    /// </summary>
    private async Task RedeemInviteAsync()
    {
        var dialog = await Dialogs.ShowAsync<RedeemInviteDialog>("Resgatar convite");
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadSideAsync();
    }
}
