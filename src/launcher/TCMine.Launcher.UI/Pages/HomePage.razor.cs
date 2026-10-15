using Microsoft.AspNetCore.Components;
using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class HomePage : ComponentBase, IDisposable
{
    /// <summary>O único caso que resta: nem sequer há servidor pareado.</summary>
    private const string WhyCannotPlay = "Pareie com um servidor para poder jogar.";

    private InstalledInstance? _active;
    private string? _error;
    private double? _fraction;

    private bool _launching;
    private bool _loading = true;
    private bool _needsChoice;
    private string? _phase;

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

    /// <summary>
    ///     Por que o botão principal está desligado, ou nulo. Numa instância de
    ///     servidor ele ENTRA no servidor, então também espera o servidor estar
    ///     no ar — a mesma regra da aba Servidores.
    /// </summary>
    private string? PlayBlockedReason =>
        Lock.Reason
        ?? (_launching ? "Abrindo o jogo…"
            : !CanPlay ? WhyCannotPlay
            : Server is { } server ? ServerEntry.Unavailable(server)
            : null);

    /// <summary>Jogo aberto ou instalação em curso (ver <see cref="ActionLock" />).</summary>
    [Inject]
    private ActionLock Lock { get; set; } = default!;

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private CatalogStore Catalog { get; set; } = default!;

    [Inject] private NewsStore NewsFeed { get; set; } = default!;

    /// <summary>
    ///     O pack da instância ativa no catálogo guardado, lido a cada desenho.
    ///     Calculado e não copiado para um campo: a cópia era o que ficava velha
    ///     quando o catálogo mudava com a tela aberta. Nulo sem rede — a ficha
    ///     continua, só sem a descrição.
    /// </summary>
    private ModpackDto? Modpack =>
        _active is null
            ? null
            : Catalog.Current?.Entries.FirstOrDefault(e => e.Modpack.Id == _active.Manifest.ModpackId)?.Modpack;

    /// <summary>
    ///     O servidor dono da instância ativa, ou nulo se ela é pessoal ou se o
    ///     catálogo não o tem agora (sem rede, ou o acesso caiu). Procurado em
    ///     todo o catálogo, e não só no pack da instância: o servidor pode ter
    ///     trocado de pack, e entrar é o que o leva à pasta nova.
    /// </summary>
    private GameServerDto? Server =>
        _active?.Manifest.ServerId is { } serverId
            ? Catalog.Current?.Entries.SelectMany(e => e.Servers).FirstOrDefault(s => s.Id == serverId)
            : null;

    /// <summary>Quantos mods a instância tem: os arquivos gerenciados em <c>mods/</c>.</summary>
    private int ModCount =>
        _active?.Manifest.ManagedFiles.Keys.Count(k => k.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
        ?? 0;

    private IReadOnlyList<ModpackNewsDto> NewsPosts =>
        _active is null ? [] : NewsFeed.For(_active.Manifest.ModpackId);

    [Inject] private GameSession Game { get; set; } = default!;

    [Inject] private IConsoleWindow ConsoleWindow { get; set; } = default!;

    [Inject] private LaunchGame Launch { get; set; } = default!;

    [Inject] private JoinServer Join { get; set; } = default!;

    [Inject] private InstallOperationState Operation { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    /// <summary>
    ///     Deixa de ouvir a sessão ao sair da tela.
    ///     Sem isto cada visita acumula um assinante num singleton que vive tanto
    ///     quanto a aplicação, e o jogo passaria a redesenhar telas mortas — uma
    ///     vez por linha de log, que são milhares.
    /// </summary>
    public void Dispose()
    {
        Game.Changed -= OnGameChanged;
        Lock.Changed -= OnGameChanged;
        Catalog.Changed -= OnGameChanged;
        NewsFeed.Changed -= OnGameChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // O jogo pode já estar a correr: o jogador abriu, foi ver os modpacks e
        // voltou. A tela tem de o encontrar assim, e não em branco.
        Game.Changed += OnGameChanged;
        Lock.Changed += OnGameChanged;

        // O catálogo e as novidades mudam sozinhos (o canal voltou, outro
        // ponto pediu para rebuscar), e a coluna da direita tem de os seguir.
        Catalog.Changed += OnGameChanged;
        NewsFeed.Changed += OnGameChanged;

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
        // importa e vem do disco, enquanto o servidor e as novidades dependem
        // de rede. Sem
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
    /// <summary>
    ///     Numa instância de servidor, "Jogar" é entrar nele: abrir sem servidor
    ///     só serviria para criar mundos solo numa pasta que segue a versão de
    ///     outro, e que pode descer de versão por baixo deles. Sem o servidor no
    ///     catálogo (sem rede), abre o jogo como uma pessoal — melhor do que
    ///     não deixar jogar.
    /// </summary>
    private Task PlayAsync()
    {
        if (Server is { } server)
            return JoinAsync(server);

        return _active is not { } active
            ? Task.CompletedTask
            : RunLaunchAsync((config, progress) =>
                Launch.HandleAsync(active, config, progress, CancellationToken.None));
    }

    /// <summary>
    ///     Abre o jogo já dentro do servidor, pela instância DELE, alinhada à
    ///     versão do servidor no caminho.
    /// </summary>
    private async Task JoinAsync(GameServerDto server)
    {
        if (Catalog.Current?.ModpackOf(server) is not { } modpack)
        {
            _error = $"O modpack de {server.Name} não está no catálogo agora. Tente de novo quando a ligação voltar.";
            return;
        }

        JoinServerResult? joined = null;

        // A trava de instalação fica presa durante a preparação da pasta do
        // servidor, senão o catálogo deixaria começar outra por baixo.
        Operation.Begin(server.Id);

        try
        {
            await RunLaunchAsync(async (config, progress) =>
            {
                joined = await Join.HandleAsync(modpack, server, config, progress, CancellationToken.None);
                return joined.Launch;
            });
        }
        finally
        {
            Operation.Finish();
        }

        if (joined is null)
            return;

        // Não impediu a entrada, mas o jogador tem de o ler: fica no mesmo
        // lugar do erro, que não some sozinho.
        if (joined.Warning is { } warning && _error is null)
            _error = warning;

        // Entrar faz da instância do servidor a ativa (o JoinServer grava a
        // escolha). Relida do disco, e não a do resultado, para o cabeçalho ter
        // o tamanho de verdade.
        if (joined.Instance is { } instance)
            _active = (await Active.CurrentAsync(CancellationToken.None)).Active ?? instance;
    }

    /// <summary>
    ///     Abrir e entrar partilham isto: andamento, erro e a trava contra dois
    ///     cliques. A instância NÃO entra aqui: abrir usa a ativa, mas entrar
    ///     usa a do servidor, que o JoinServer acha (ou instala) sozinho.
    /// </summary>
    private async Task RunLaunchAsync(
        Func<LauncherConfig, IProgress<GameLaunchProgress>, Task<GameLaunchResult>> launch)
    {
        if (Shell.Pairing?.Config is not { } config || _launching)
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
            var result = await launch(config, progress);

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
    ///     O que vem da rede: a descrição do pack, o servidor e as novidades.
    ///     Falhar aqui é silencioso de propósito. Esta tela tem de servir
    ///     offline, e um erro vermelho sobre o catálogo faria parecer que a
    ///     instância instalada também está com problema — quando ela está inteira
    ///     no disco, e o botão de jogar continua a funcionar.
    /// </summary>
    private async Task LoadSideAsync()
    {
        if (_active is null)
            return;

        await Catalog.EnsureLoadedAsync();

        // Depois do catálogo, e numa chamada à parte: as novidades são o que
        // menos importa nesta tela, e falhar a buscá-las não pode levar o
        // catálogo consigo.
        await NewsFeed.RefreshAsync(_active.Manifest.ModpackId);
    }
}
