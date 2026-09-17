using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class HomePage : ComponentBase, IDisposable
{
    private InstalledInstance? _active;
    private bool _needsChoice;
    private bool _loading = true;
    private IReadOnlyList<GameServerDto> _servers = [];

    private bool _launching;
    private string? _phase;
    private double? _fraction;
    private string? _error;

    /// <summary>
    ///     Há conta e pareamento para abrir o jogo.
    ///     Verificado aqui e não dentro do caso de uso porque o botão precisa
    ///     nascer desabilitado: oferecer "Jogar" a quem não entrou faria o clique
    ///     existir só para recusar.
    /// </summary>
    private bool CanPlay => Shell.Player is not null && Shell.Pairing?.Config is not null;

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private GameSession Game { get; set; } = default!;

    [Inject] private LaunchGame Launch { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    /// <summary>
    ///     Deixa de ouvir a sessão ao sair da tela.
    ///     Sem isto cada visita acumula um assinante num singleton que vive tanto
    ///     quanto a aplicação, e o jogo passaria a redesenhar telas mortas — uma
    ///     vez por linha de log, que são milhares.
    /// </summary>
    public void Dispose()
    {
        Game.Changed -= AoMudarOJogo;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // O jogo pode já estar a correr: o jogador abriu, foi ver os modpacks e
        // voltou. A tela tem de o encontrar assim, e não em branco.
        Game.Changed += AoMudarOJogo;

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
        await CarregarServidoresAsync();
    }

    /// <summary>
    ///     Redesenha quando o jogo escreve ou fecha.
    ///     Vem de um fio de fundo, por isso o InvokeAsync: tocar no estado do
    ///     componente fora do circuito é o caminho para uma tela que congela.
    /// </summary>
    private void AoMudarOJogo() => InvokeAsync(StateHasChanged);

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
    private async Task PlayAsync()
    {
        if (_active is null || Shell.Player is not { } jogador || Shell.Pairing?.Config is not { } config)
            return;

        _launching = true;
        _error = null;
        _fraction = null;

        var andamento = new Progress<GameLaunchProgress>(p =>
        {
            _phase = p.Phase;
            _fraction = p.Fraction;

            // Progress<T> chega no contexto do circuito mas fora do ciclo de
            // render: sem isto a barra fica parada até o próximo evento da tela.
            InvokeAsync(StateHasChanged);
        });

        try
        {
            var resultado = await Launch.HandleAsync(
                _active, config, jogador, andamento, CancellationToken.None);

            if (!resultado.Started)
                _error = resultado.Message;
        }
        finally
        {
            _launching = false;
            _phase = null;
            _fraction = null;
        }
    }

    /// <summary>
    ///     Os servidores do pack ativo, quando há ligação.
    ///     Falhar aqui é silencioso de propósito: a tela de jogar tem de servir
    ///     offline, e um erro vermelho sobre o catálogo faria parecer que a
    ///     instância instalada também está com problema — quando ela está
    ///     inteira no disco.
    /// </summary>
    private async Task CarregarServidoresAsync()
    {
        if (_active is null || Shell.Pairing?.Config is not { } config)
            return;

        var catalogo = await Catalog.HandleAsync(config.ServerUrl, CancellationToken.None);

        if (catalogo.Failed)
            return;

        _servers = catalogo.Entries
            .FirstOrDefault(e => e.Modpack.Id == _active.Manifest.ModpackId)
            ?.Servers ?? [];

        StateHasChanged();
    }
}
