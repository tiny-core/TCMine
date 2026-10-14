using System.Globalization;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Conta os jogadores com o <c>mc-monitor status</c> da própria imagem itzg,
///     dentro do container.
///     O <c>mc-monitor</c> pergunta pelo protocolo da lista de servidores (o mesmo
///     que o jogo usa para mostrar "3/20" no menu multijogador) e é o que a
///     verificação de saúde do container já roda. Não abre RCON, então não deixa
///     rastro no console do jogo, e não depende da senha do RCON.
/// </summary>
public sealed partial class DockerPlayerCountProbe(
    DockerApiClient docker,
    ILogger<DockerPlayerCountProbe> logger) : IPlayerCountProbe
{
    private readonly ILogger<DockerPlayerCountProbe> _logger = logger;

    /// <summary>
    ///     O comando. <c>--show-player-count</c> faz a saída ser só o número;
    ///     o prazo curto impede que um servidor travado segure a coleta inteira
    ///     (o padrão do mc-monitor é 15 s, o intervalo da coleta).
    /// </summary>
    public static readonly IReadOnlyList<string> Command =
        ["mc-monitor", "status", "--show-player-count", "--timeout", "5s"];

    public async Task<int?> GetOnlineAsync(Guid gameServerId, CancellationToken ct)
    {
        try
        {
            return ParseCount(await docker.ExecAsync($"tcmine-{gameServerId}", Command, ct));
        }
        catch (HttpRequestException ex)
        {
            // Container sumido ou Docker fora: a contagem só some, e quem
            // percebe o problema de verdade é o reconciliador de status.
            LogFailed(ex, gameServerId);
            return null;
        }
    }

    /// <summary>
    ///     A saída só vale se for UM número. Quando o servidor não responde, o
    ///     mc-monitor escreve a mensagem de erro no mesmo fluxo (o exec usa TTY)
    ///     — e uma mensagem com um número no meio não pode virar contagem.
    /// </summary>
    public static int? ParseCount(string? output) =>
        int.TryParse(output?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Não foi possível contar os jogadores do servidor {ServerId}.")]
    private partial void LogFailed(Exception ex, Guid serverId);
}
