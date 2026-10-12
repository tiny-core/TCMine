using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     O catálogo (modpacks e servidores) guardado num lugar só, com aviso a
///     quem estiver na tela.
///     Antes cada página buscava o seu ao abrir, então só atualizava quando o
///     jogador navegava: com o servidor de volta depois de uma queda, a coluna
///     de servidores da tela Jogar ficava vazia até se trocar de página.
///     Rebusca sozinho quando o canal fica de pé (entrar, voltar de uma queda),
///     e quem mais souber que algo mudou chama <see cref="RefreshAsync" />.
///     Uma busca de cada vez: pedidos que chegam durante uma busca não abrem
///     outra em paralelo, mas também não se perdem — viram UMA busca a seguir,
///     porque podem trazer uma mudança que a busca em curso já não vê.
/// </summary>
public sealed class CatalogStore : IDisposable
{
    private readonly Lock _gate = new();
    private readonly LoadCatalog _load;
    private readonly LauncherShellState _shell;

    private bool _again;
    private LinkStatus _lastLink;
    private Task? _running;

    public CatalogStore(LoadCatalog load, LauncherShellState shell)
    {
        _load = load;
        _shell = shell;
        _lastLink = shell.Link;

        _shell.Changed += OnShellChanged;
    }

    /// <summary>O último catálogo que chegou inteiro. Nulo antes da primeira busca.</summary>
    public CatalogView? Current { get; private set; }

    /// <summary>
    ///     O motivo da última busca que falhou, ou nulo quando a última deu
    ///     certo. Uma falha não apaga o <see cref="Current" />: um catálogo de
    ///     há um minuto serve melhor do que uma tela vazia.
    /// </summary>
    public string? Error { get; private set; }

    public bool IsLoading { get; private set; }

    /// <summary>
    ///     O que uma tela deve mostrar: o catálogo que houver, ou a falha quando
    ///     nunca houve nenhum. Nulo enquanto a primeira busca não termina.
    /// </summary>
    public CatalogView? View => Current ?? (Error is { } error ? CatalogView.Failure(error) : null);

    public void Dispose() => _shell.Changed -= OnShellChanged;

    /// <summary>Uma mudança em qualquer coisa acima. Pode vir de qualquer thread.</summary>
    public event Action? Changed;

    /// <summary>Busca só se ainda não há catálogo — é o que uma tela chama ao abrir.</summary>
    public Task EnsureLoadedAsync() => Current is null ? RefreshAsync() : Task.CompletedTask;

    public Task RefreshAsync()
    {
        lock (_gate)
        {
            if (_running is not null)
            {
                _again = true;
                return _running;
            }

            // Task.Run: quem chama (um evento do canal, um clique) não espera
            // pela rede nem roda a busca dentro do próprio aviso.
            _running = Task.Run(RunAsync);

            return _running;
        }
    }

    /// <summary>A busca em curso, ou uma tarefa concluída se não há nenhuma.</summary>
    public Task WhenIdle()
    {
        lock (_gate)
            return _running ?? Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        IsLoading = true;
        Changed?.Invoke();

        while (true)
        {
            await LoadOnceAsync();

            lock (_gate)
            {
                if (!_again)
                {
                    // Dentro do lock: um pedido que chegue a seguir já encontra
                    // a busca encerrada e abre outra, em vez de se pendurar
                    // nesta, que não voltaria a buscar.
                    _running = null;
                    IsLoading = false;

                    break;
                }

                _again = false;
            }
        }

        Changed?.Invoke();
    }

    private async Task LoadOnceAsync()
    {
        // Sem sessão o hub recusa: buscar só encheria o log de recusas.
        if (!_shell.IsSignedIn || _shell.Pairing?.Config is not { } config)
            return;

        var view = await _load.HandleAsync(config.ServerUrl, CancellationToken.None);

        if (view.Failed)
            Error = view.Error;
        else
        {
            Current = view;
            Error = null;
        }

        Changed?.Invoke();
    }

    private void OnShellChanged()
    {
        if (!_shell.IsSignedIn && (Current is not null || Error is not null))
        {
            // O catálogo é o que ESTA conta pode ver. Quem entrar a seguir não
            // pode herdar servidores a que não tem acesso, nem por um instante.
            Current = null;
            Error = null;
            Changed?.Invoke();
        }

        var link = _shell.Link;
        var previous = _lastLink;
        _lastLink = link;

        if (link is LinkStatus.Up && previous is not LinkStatus.Up)
            _ = RefreshAsync();
    }
}
