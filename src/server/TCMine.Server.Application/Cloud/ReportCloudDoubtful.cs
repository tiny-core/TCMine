using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Operações que um servidor de jogo pode ter perdido numa queda (plano §5):
///     vão para a fila "Em dúvida" do painel. Nunca devolvidas sozinhas: só o
///     dono sabe se o mundo gravou o item.
/// </summary>
public sealed partial class ReportCloudDoubtful(
    ICloudGovernanceRepository governance,
    ILogger<ReportCloudDoubtful> logger)
{
    public async Task<CloudCallResult<bool>> HandleAsync(CloudServerContext ctx, CloudDoubtfulRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ReportId) || request.ReportId.Length > 64)
            return CloudCallResult<bool>.Invalid("Id do relatório inválido.");
        if (request.Operations.Count > CloudProtocol.MaxOpsPerBatch)
            return CloudCallResult<bool>.Invalid("Operações demais num relatório.");

        var rows = new List<CloudDoubtfulOperation>(request.Operations.Count);
        for (var i = 0; i < request.Operations.Count; i++)
        {
            var op = request.Operations[i];
            var player = CloudChannel.NormalizePlayerUuid(op.PlayerUuid);
            if (player is null || op.Amount <= 0 || op.Fingerprint is not { Length: CloudItemType.FingerprintLength }
                || !Enum.TryParse<CloudDoubtfulKind>(op.Kind, true, out var kind))
                return CloudCallResult<bool>.Invalid($"Operação {i} inválida.");

            rows.Add(new CloudDoubtfulOperation
            {
                VaultId = ctx.VaultId,
                ServerId = ctx.ServerId,
                ReportId = request.ReportId,
                Index = i,
                PlayerUuid = player,
                ChannelId = op.ChannelId,
                Fingerprint = op.Fingerprint,
                Kind = kind,
                Amount = op.Amount
            });
        }

        await governance.AddDoubtfulAsync(rows, ct);
        if (rows.Count > 0)
            LogReported(ctx.ServerId, rows.Count);
        return CloudCallResult<bool>.Ok(true);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Nuvem: o servidor {ServerId} reportou {Count} operação(ões) em dúvida após uma queda.")]
    private partial void LogReported(Guid serverId, int count);
}

/// <summary>
///     Itens que o mod recusou por parecerem guardar dados no mundo: entram na
///     fila de suspeitos para o dono permitir ou bloquear de vez.
/// </summary>
public sealed class ReportCloudSuspects(ICloudGovernanceRepository governance, TimeProvider clock)
{
    public const int MaxItems = 200;

    public async Task<CloudCallResult<bool>> HandleAsync(CloudServerContext ctx, CloudSuspectsRequest request,
        CancellationToken ct)
    {
        if (request.Items.Count > MaxItems)
            return CloudCallResult<bool>.Invalid("Itens demais num relatório.");

        var items = new List<(string, string, long)>();
        foreach (var item in request.Items)
        {
            var id = CloudItemRule.NormalizePattern(CloudRuleScope.Item, item.ItemId);
            if (id is null)
                return CloudCallResult<bool>.Invalid($"Id de item inválido: {item.ItemId}");
            items.Add((id, item.Evidence ?? "", Math.Clamp(item.Attempts, 1, 1_000_000)));
        }

        if (items.Count > 0)
            await governance.RecordSuspectsAsync(ctx.VaultId, items, clock.GetUtcNow(), ct);
        return CloudCallResult<bool>.Ok(true);
    }
}
