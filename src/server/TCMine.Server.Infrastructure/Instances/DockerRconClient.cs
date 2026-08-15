using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Envia comandos ao servidor de jogo por <c>rcon-cli</c> dentro do próprio
///     container.
///     A alternativa seria abrir a porta 25575 no host e falar o protocolo RCON
///     por TCP. Não vale: exporia um canal de controle total do servidor na rede
///     da máquina, obrigaria a guardar e transmitir o segredo a cada comando, e
///     só funcionaria em containers recriados depois da mudança. Por dentro, o
///     <c>rcon-cli</c> da imagem itzg já lê a senha da variável de ambiente — o
///     segredo nunca sai do container, e containers antigos passam a funcionar
///     sem recriar nada.
/// </summary>
public sealed partial class DockerRconClient(
    DockerApiClient docker,
    ILogger<DockerRconClient> logger) : IRconClient
{
    private readonly ILogger<DockerRconClient> _logger = logger;

    public async Task<string> ExecuteAsync(Guid gameServerId, string rawCommand, CancellationToken ct)
    {
        // rcon-cli recebe o comando como argumentos separados; não há shell no
        // meio, então não há injeção a temer aqui — mas o comando ainda é
        // arbitrário para o jogo, e quem chama já passou pela autorização.
        var argumentos = rawCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (argumentos.Length is 0)
            return "";

        try
        {
            var saida = await docker.ExecAsync(
                $"tcmine-{gameServerId}", ["rcon-cli", .. argumentos], ct);

            return saida.Trim();
        }
        catch (HttpRequestException ex)
        {
            LogFailed(ex, gameServerId, rawCommand);
            throw new RconUnavailableException($"Não foi possível falar com o servidor: {ex.Message}", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao executar '{Command}' no servidor {ServerId}.")]
    private partial void LogFailed(Exception ex, Guid serverId, string command);
}
