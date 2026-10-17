using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Filtros que a tela de auditoria manda, ainda crus (o UUID do jogador pode
///     vir com hífens e espaços). Tudo nulo = a nuvem inteira.
/// </summary>
public sealed record CloudLedgerFilter(
    string? PlayerUuid = null,
    string? Item = null,
    CloudLedgerSource? Source = null,
    DateTimeOffset? From = null,
    DateTimeOffset? Until = null);

/// <summary>
///     Decisões tomadas no painel desta nuvem (quem aplicou quarentena, estornou,
///     descongelou...), uma página por vez. Separado dos movimentos de itens:
///     são listas de tamanhos e perguntas diferentes, e juntas uma esperava a
///     outra e dividiam o mesmo limite fixo.
/// </summary>
public sealed class ListCloudAdminActions(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope)
{
    public const int PageSize = 50;

    public async Task<Result<CursorPage<CloudAdminAuditEntry>>> HandleAsync(Guid vaultId, Guid? before,
        CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        return access.Succeeded
            ? Result<CursorPage<CloudAdminAuditEntry>>.Success(
                await governance.ListAuditAsync(vaultId, before, PageSize, ct))
            : Result<CursorPage<CloudAdminAuditEntry>>.Fail(access.Error!);
    }
}

/// <summary>
///     Movimentos de itens desta nuvem (o ledger), uma página por vez, com os
///     filtros resolvidos no banco. Sem jogador, é a visão da nuvem inteira —
///     barata porque o ledger guarda a nuvem em cada linha.
/// </summary>
public sealed class ListCloudLedger(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope)
{
    public const int PageSize = 50;

    /// <summary>Teto do texto de busca do item: o tamanho máximo de um id de item no banco.</summary>
    public const int MaxItemSearchLength = 256;

    public const string InvalidPlayer = "UUID de jogador inválido.";

    public async Task<Result<CursorPage<CloudLedgerView>>> HandleAsync(Guid vaultId, CloudLedgerFilter filter,
        Guid? before, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result<CursorPage<CloudLedgerView>>.Fail(access.Error!);

        // UUID errado é recusado, e não ignorado: o código antigo tratava um
        // UUID digitado errado como "sem filtro" e mostrava a nuvem inteira —
        // o dono achava que estava vendo um jogador só.
        string? player = null;
        if (!string.IsNullOrWhiteSpace(filter.PlayerUuid))
        {
            player = CloudChannel.NormalizePlayerUuid(filter.PlayerUuid);
            if (player is null)
                return Result<CursorPage<CloudLedgerView>>.Fail(InvalidPlayer);
        }

        var item = string.IsNullOrWhiteSpace(filter.Item) ? null : filter.Item.Trim();
        if (item is { Length: > MaxItemSearchLength })
            return Result<CursorPage<CloudLedgerView>>.Fail("Busca de item longa demais.");

        if (filter.From is { } periodStart && filter.Until is { } periodEnd && periodStart >= periodEnd)
            return Result<CursorPage<CloudLedgerView>>.Fail("O início do período tem de ser antes do fim.");

        var query = new CloudLedgerQuery(vaultId, player, item, filter.Source, filter.From, filter.Until, before,
            PageSize);
        return Result<CursorPage<CloudLedgerView>>.Success(await governance.ListLedgerAsync(query, ct));
    }
}
