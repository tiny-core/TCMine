namespace TCMine.Server.Infrastructure.Docker;

public sealed class DockerOptions
{
    /// <summary>
    ///     Endpoint do daemon. Linux: "unix:///var/run/docker.sock".
    ///     Windows: "npipe://./pipe/docker_engine".
    /// </summary>
    public string Endpoint { get; set; } = "unix:///var/run/docker.sock";

    /// <summary>Versão da API do Docker no path das rotas. 1.45 = Docker 26+.</summary>
    public string ApiVersion { get; set; } = "v1.45";
}
