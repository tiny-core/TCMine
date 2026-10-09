using System.Diagnostics;

namespace TCMine.Server.Web.Diagnostics;

/// <summary>
///     TEMPORÁRIO — linha de base da refatoração (docs/BASELINE.md).
///     Mede a abertura de uma página: carga dos dados e renderização.
///     Removido no fim da fase 8.
/// </summary>
internal sealed partial class PageLoadTimer
{
    private readonly ILogger<PageLoadTimer> _logger;
    private long _loadedAt;
    private string? _page;
    private long _startedAt;

    public PageLoadTimer(ILogger<PageLoadTimer> logger)
    {
        _logger = logger;
    }

    public void Start(string page)
    {
        _page = page;
        _startedAt = Stopwatch.GetTimestamp();
        _loadedAt = 0;
    }

    public void Loaded() => _loadedAt = Stopwatch.GetTimestamp();

    /// <summary>
    ///     Chamado em todo OnAfterRender; só loga no primeiro depois da carga.
    /// </summary>
    public void Rendered()
    {
        if (_page is null || _loadedAt == 0)
            return;

        var loadMs = (long)Stopwatch.GetElapsedTime(_startedAt, _loadedAt).TotalMilliseconds;
        var renderMs = (long)Stopwatch.GetElapsedTime(_loadedAt).TotalMilliseconds;

        LogPageOpened(_page, loadMs, renderMs);
        _page = null;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Abertura de página: {Page} — carga {LoadMs} ms, renderização {RenderMs} ms.")]
    private partial void LogPageOpened(string page, long loadMs, long renderMs);
}
