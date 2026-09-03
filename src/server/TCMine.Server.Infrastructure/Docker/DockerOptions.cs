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

    /// <summary>
    ///     Prazo para ABRIR o socket — não para a requisição inteira.
    ///     Curto de propósito: com o daemon no ar, o socket responde em
    ///     milissegundos; se não responde em segundos, ele não está lá e esperar
    ///     mais não muda nada. O prazo longo continua sendo o do HttpClient, que
    ///     é quem cobre operações legitimamente demoradas (baixar imagem).
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
