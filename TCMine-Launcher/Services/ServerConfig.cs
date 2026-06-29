namespace TCMine_Launcher.Services;

/// <summary>
/// Aponta o launcher para o TCMine Server do qual ele depende inteiramente (catálogo, login, jars).
/// A URL vem **injetada no build** (<see cref="AppConfig.ServerUrl"/>) — em produção o servidor
/// compila o launcher e injeta a sua URL/IP. Sem valor injetado (builds de dev sem
/// <c>Client.props</c>), cai no servidor local.
/// </summary>
public sealed class ServerConfig
{
    // Fallback só para dev: builds de produção sempre têm AppConfig.ServerUrl injetado.
    private const string DevFallback = "https://localhost:7002";

    public string BaseUrl { get; } = AppConfig.ServerUrl ?? DevFallback;

    /// <summary>Resolve um caminho relativo contra a base do servidor.</summary>
    public Uri Resolve(string path) => new(new Uri(BaseUrl), path);
}
