namespace TCMine_Launcher.Infrastructure;

/// <summary>
/// Aponta o launcher para o TCMine Server (catálogo, login, jars). A URL vem injetada no build
/// (<see cref="AppConfig.ServerUrl"/>); sem valor (dev), cai no servidor local.
/// </summary>
public sealed class ServerConfig
{
    private const string DevFallback = "https://localhost:7002";

    public string BaseUrl { get; } = AppConfig.ServerUrl ?? DevFallback;

    public Uri Resolve(string path) => new(new Uri(BaseUrl), path);
}
