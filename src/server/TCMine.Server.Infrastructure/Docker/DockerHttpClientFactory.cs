using System.IO.Pipes;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace TCMine.Server.Infrastructure.Docker;

/// <summary>
///     Cria um HttpClient cujo transporte é o socket do Docker (Unix ou named pipe),
///     não TCP. O daemon fala HTTP por cima do socket, então após conectado é
///     REST normal. A BaseAddress "http://localhost" é fictícia — o roteamento é o
///     socket, não o host.
/// </summary>
public sealed class DockerHttpClientFactory(IOptions<DockerOptions> options)
{
    private readonly DockerOptions _options = options.Value;

    public HttpClient Create()
    {
        var (scheme, path) = ParseEndpoint(_options.Endpoint);

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                // Conectar tem prazo próprio, e ele existe por um comportamento
                // específico do named pipe: NamedPipeClientStream.ConnectAsync(ct)
                // espera INDEFINIDAMENTE o pipe aparecer. Com o Docker Desktop
                // parado, a chamada só voltava quando o timeout padrão do
                // HttpClient a matava — cem segundos depois. Foi assim que a
                // página de Definições passou a levar 100s para carregar numa
                // máquina sem Docker no ar, com o `catch` do caso de uso tratando
                // a exceção certinho e ninguém percebendo a espera.
                using var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
                prazo.CancelAfter(_options.ConnectTimeout);

                try
                {
                    if (scheme == "npipe")
                    {
                        // Windows: named pipe. O path vem como "./pipe/docker_engine";
                        // o NamedPipeClientStream quer só o nome do pipe.
                        var pipeName = path.Replace("./pipe/", "").Replace("/", "\\");
                        var pipe = new NamedPipeClientStream(
                            ".", pipeName, PipeDirection.InOut,
                            PipeOptions.Asynchronous);
                        await pipe.ConnectAsync(prazo.Token);
                        return pipe;
                    }

                    // Linux: Unix domain socket.
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), prazo.Token);
                    return new NetworkStream(socket, true);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // HttpRequestException, e não a de cancelamento: para quem
                    // chama isto é o daemon inacessível, que é o que os casos de
                    // uso já sabem tratar. Um TaskCanceledException aqui pareceria
                    // o usuário ter desistido da operação.
                    throw new HttpRequestException(
                        $"O Docker não respondeu em {_options.ConnectTimeout.TotalSeconds:0.#}s " +
                        $"no endpoint {_options.Endpoint}.");
                }
            }
        };

        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    // "unix:///var/run/docker.sock" → ("unix", "/var/run/docker.sock")
    // "npipe://./pipe/docker_engine" → ("npipe", "./pipe/docker_engine")
    private static (string Scheme, string Path) ParseEndpoint(string endpoint)
    {
        var idx = endpoint.IndexOf("://", StringComparison.Ordinal);
        if (idx < 0)
            throw new InvalidOperationException($"Endpoint Docker inválido: {endpoint}");

        var scheme = endpoint[..idx];
        var path = endpoint[(idx + 3)..];
        return (scheme, path);
    }
}
