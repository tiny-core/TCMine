using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class ShellLayout : LayoutComponentBase, IDisposable
{
    private bool _updateFailed;
    private bool _updating;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private ServerPairing Pairing { get; set; } = default!;

    [Inject] private PostPairingRoute PostPairing { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private ActionLock Lock { get; set; } = default!;

    [Inject] private GameSession Game { get; set; } = default!;

    [Inject] private LinkSupervisor Link { get; set; } = default!;

    [Inject] private UpdateLauncher Updater { get; set; } = default!;

    [Inject] private ResumeRunningGame ResumeGame { get; set; } = default!;

    /// <summary>
    ///     Pedido só para nascer: o coordenador ouve o jogo sozinho (ver
    ///     <see cref="GameWindowCoordinator" />), e o layout é o primeiro a
    ///     existir na janela.
    /// </summary>
    [Inject]
    private GameWindowCoordinator WindowCoordinator { get; set; } = default!;

    // TEMPORÁRIO (linha de base, sai no fim da fase 8).
    [Inject] private ILogger<ShellLayout> Logger { get; set; } = default!;

    /// <summary>
    ///     Falhar aqui nunca impede o arranque: no pior caso, o launcher só não
    ///     sabe do jogo que ficou aberto.
    /// </summary>
    private async Task TryResumeGameAsync()
    {
        try
        {
            await ResumeGame.HandleAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LogResumeFailed(Logger, ex);
        }
    }

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        Lock.Changed -= OnShellChanged;
        Link.NavigationRequested -= OnNavigationRequested;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     O arranque mora no layout, e não numa página, porque ele acontece uma
    ///     vez por sessão: o layout não é reconstruído ao navegar, e a checagem
    ///     não pode repetir a cada troca de tela.
    ///     São dois passos em ordem, e a ordem importa: sem servidor não há a
    ///     quem pedir sessão, e o client id do Azure — que a autenticação exige —
    ///     vem justamente da configuração que o pareamento gravou.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        Shell.Changed += OnShellChanged;
        Lock.Changed += OnShellChanged;

        // Antes do arranque: é o supervisor que abre o canal assim que houver
        // sessão, e a sessão pode ser retomada já nos passos abaixo.
        Link.NavigationRequested += OnNavigationRequested;
        Link.Attach();

        // Antes de tudo o resto, e sem rede: um jogo deixado aberto por um
        // launcher anterior tem de ser reencontrado ANTES de qualquer tela
        // oferecer "Jogar" — senão um clique abriria uma segunda cópia.
        await TryResumeGameAsync();

        Shell.BeginCheck();

        // TEMPORÁRIO (linha de base): como o arranque terminou. Sem isto, uma
        // abertura que para no login entra na mesma mediana de uma que
        // autentica — e são caminhos com custos diferentes.
        var outcome = "erro";

        try
        {
            var pairing = await Pairing.ResumeAsync(CancellationToken.None);
            Shell.Apply(pairing);

            if (!pairing.IsPaired)
            {
                outcome = "sem pareamento";
                Navigation.NavigateTo("/pair");
                return;
            }

            // Servidor fora do ar não tem como emitir sessão — e mandar para o
            // login nesse caso prendia o jogador: a tela pede a conta Microsoft,
            // o login precisa do servidor para trocar a prova por sessão, e o
            // servidor é justamente o que não está lá. Sem saída, com o disco
            // cheio de coisa instalada do outro lado.
            // Offline fica-se onde se está. O que precisa de rede aparece
            // desligado, com o motivo, em vez de levar a lado nenhum.
            if (!pairing.IsOnline)
            {
                outcome = "offline";
                return;
            }

            var destino = await PostPairing.ResolveAsync(pairing, CancellationToken.None);
            outcome = destino switch
            {
                "/login" => "login",
                null => "vai atualizar",
                _ => "com sessão"
            };

            // Só navega quando manda para o login: com sessão já válida, o
            // arranque não deve atropelar uma rota profunda que o jogador tenha
            // pedido (ex.: reabrir direto na aba de mods de uma instância).
            // destino nulo = vai reiniciar para se atualizar.
            if (destino is "/login")
                Navigation.NavigateTo(destino);
        }
        finally
        {
            // No finally: uma exceção aqui deixaria a janela girando para sempre,
            // que é o pior desfecho possível para um arranque.
            Shell.FinishStartup();

            // TEMPORÁRIO (linha de base, sai no fim da fase 8). No finally
            // porque é aqui que o arranque termina, com ou sem servidor.
            var usableMs = StartupClock.ElapsedMs;
            LogStartupFinished(Logger, usableMs, outcome);
        }
    }

    private void OnShellChanged() => InvokeAsync(StateHasChanged);

    /// <summary>
    ///     O supervisor pede a rota e a moldura navega: ele vive fora da árvore
    ///     de componentes e chama de qualquer thread, e navegar só é seguro no
    ///     contexto do circuito.
    /// </summary>
    private void OnNavigationRequested(string route) => InvokeAsync(() => Navigation.NavigateTo(route));

    private string UpdateNotice(string version) =>
        _updateFailed ? "Não foi possível atualizar o launcher agora. Tente de novo."
        : Lock.Reason is { } reason ? $"Há uma versão nova do launcher ({version}). {reason}"
        : $"Há uma versão nova do launcher ({version}).";

    /// <summary>
    ///     Atualiza porque o jogador pediu. Recusa com o jogo aberto ou uma
    ///     instalação em curso: reiniciar aí deixaria o jogo órfão, ou uma
    ///     instância pela metade.
    /// </summary>
    private async Task UpdateNowAsync()
    {
        if (_updating || Lock.IsLocked)
            return;

        _updating = true;
        _updateFailed = false;

        try
        {
            // Verdadeiro quer dizer que o processo vai reiniciar: não há mais
            // nada a fazer aqui. Falso é falha — não havia como chegar ao botão
            // sem uma versão à espera.
            _updateFailed = !await Updater.HandleAsync(Shell.Pairing?.Server, CancellationToken.None);
        }
        finally
        {
            _updating = false;
        }
    }

    // Estático com ILogger explícito, como no LoggingErrorBoundary: o gerador
    // de [LoggerMessage] só acha campo, e um componente recebe por [Inject].
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Arranque: primeira tela utilizável em {ElapsedMs} ms ({Outcome}).")]
    private static partial void LogStartupFinished(ILogger logger, long elapsedMs, string outcome);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível reencontrar o jogo deixado aberto; o arranque segue.")]
    private static partial void LogResumeFailed(ILogger logger, Exception ex);
}
