using Microsoft.Extensions.Logging;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Operações que um servidor de jogo pode ter perdido numa queda (plano §5).
///     Por ora só registradas no log; a tabela e a tela "Em dúvida" entram com a
///     fatia do painel. Nunca devolvidas sozinhas: só o dono sabe se o mundo
///     gravou o item.
/// </summary>
public sealed partial class ReportCloudDoubtful(ILogger<ReportCloudDoubtful> logger)
{
    public CloudCallResult<bool> Handle(CloudServerContext ctx, CloudDoubtfulRequest request)
    {
        if (request.Operations.Count > CloudProtocol.MaxOpsPerBatch)
            return CloudCallResult<bool>.Invalid("Operações demais num relatório.");

        foreach (var op in request.Operations)
            LogDoubtful(ctx.ServerId, op.PlayerUuid, op.ChannelId, op.Fingerprint, op.Kind, op.Amount);

        return CloudCallResult<bool>.Ok(true);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Nuvem: operação em dúvida no servidor {ServerId}: jogador {Player}, canal {ChannelId}, item {Fingerprint}, {Kind} {Amount}.")]
    private partial void LogDoubtful(Guid serverId, string player, Guid channelId, string fingerprint, string kind, long amount);
}
