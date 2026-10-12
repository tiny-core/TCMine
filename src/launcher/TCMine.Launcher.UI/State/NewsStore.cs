using System.Collections.Concurrent;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     As novidades publicadas, por modpack, guardadas para a tela não as
///     buscar a cada visita e rebuscadas quando o canal volta.
///     Só os packs que alguém pediu: o launcher não tem por que descarregar as
///     novidades de um catálogo inteiro para mostrar as de um pack.
///     Falhar é silencioso, como era na tela: as novidades são o que menos
///     importa onde aparecem, e um erro sobre elas faria parecer que o resto
///     também está com problema.
/// </summary>
public sealed class NewsStore : IDisposable
{
    private readonly IServerConnection _connection;
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ModpackNewsDto>> _posts = new();
    private readonly LauncherShellState _shell;

    private LinkStatus _lastLink;

    public NewsStore(IServerConnection connection, LauncherShellState shell)
    {
        _connection = connection;
        _shell = shell;
        _lastLink = shell.Link;

        _shell.Changed += OnShellChanged;
    }

    public void Dispose() => _shell.Changed -= OnShellChanged;

    /// <summary>Pode vir de qualquer thread.</summary>
    public event Action? Changed;

    /// <summary>As novidades guardadas do pack; vazio quando ainda não chegaram.</summary>
    public IReadOnlyList<ModpackNewsDto> For(Guid modpackId) =>
        _posts.TryGetValue(modpackId, out var posts) ? posts : [];

    public async Task RefreshAsync(Guid modpackId)
    {
        if (!_shell.IsSignedIn)
            return;

        try
        {
            _posts[modpackId] = await _connection.GetNewsAsync(modpackId, CancellationToken.None);
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem novidades a coluna simplesmente não as mostra.
        }
    }

    private void OnShellChanged()
    {
        if (!_shell.IsSignedIn && !_posts.IsEmpty)
        {
            // Rascunhos não chegam aqui, mas o que cada conta vê depende do
            // acesso dela: quem entrar a seguir começa do zero.
            _posts.Clear();
            Changed?.Invoke();
        }

        var link = _shell.Link;
        var previous = _lastLink;
        _lastLink = link;

        if (link is not LinkStatus.Up || previous is LinkStatus.Up)
            return;

        foreach (var modpackId in _posts.Keys)
            _ = RefreshAsync(modpackId);
    }
}
