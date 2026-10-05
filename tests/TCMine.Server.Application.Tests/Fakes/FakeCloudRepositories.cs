using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>Nuvens e canais em memória para os testes de permissão do painel da nuvem.</summary>
internal sealed class FakeCloudAdminRepository : ICloudAdminRepository
{
    public List<CloudVault> Vaults { get; } = [];
    public List<CloudChannel> Channels { get; } = [];

    public Task AddVaultAsync(CloudVault vault, CancellationToken ct)
    {
        Vaults.Add(vault);
        return Task.CompletedTask;
    }

    public Task UpdateVaultAsync(CloudVault vault, CancellationToken ct) => Task.CompletedTask;

    public Task<CloudVault?> GetVaultAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Vaults.FirstOrDefault(v => v.Id == id));

    public Task<IReadOnlyList<CloudVaultSummary>> ListVaultsAsync(Guid? ownerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CloudVaultSummary>>(
        [
            .. Vaults.Where(v => ownerId is null || v.OwnerId == ownerId)
                .Select(v => new CloudVaultSummary(v.Id, v.Name, v.OwnerId, v.IsEnabled, 0, 0))
        ]);

    public Task<IReadOnlyList<CloudServerKeyView>> ListActiveKeysAsync(IReadOnlyCollection<Guid> serverIds,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<CloudServerKeyView>>([]);

    public Task<IReadOnlyList<CloudPlayerView>> ListPlayersAsync(Guid vaultId, string? search, int limit,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<CloudPlayerView>>([]);

    public Task<IReadOnlyList<CloudBalanceView>> ListChannelBalancesAsync(Guid channelId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CloudBalanceView>>([]);

    public Task<CloudChannel?> GetChannelAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Channels.FirstOrDefault(c => c.Id == id));

    public Task UpdateChannelAsync(CloudChannel channel, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Chaves de servidor em memória.</summary>
internal sealed class FakeCloudCredentialRepository : ICloudCredentialRepository
{
    public List<CloudServerCredential> Credentials { get; } = [];

    public Task<CloudServerCredential?> FindByPrefixAsync(string prefix, CancellationToken ct) =>
        Task.FromResult(Credentials.FirstOrDefault(c => c.KeyPrefix == prefix));

    public Task<CloudServerCredential?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Credentials.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<CloudServerCredential>> ListActiveByServerAsync(Guid gameServerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CloudServerCredential>>(
            [.. Credentials.Where(c => c.GameServerId == gameServerId && c.IsActive)]);

    public Task AddAsync(CloudServerCredential credential, CancellationToken ct)
    {
        Credentials.Add(credential);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CloudServerCredential credential, CancellationToken ct) => Task.CompletedTask;

    public Task<CloudVault?> GetVaultAsync(Guid vaultId, CancellationToken ct) => Task.FromResult<CloudVault?>(null);
}

/// <summary>Só a auditoria (o resto dos testes de permissão não chega à governança).</summary>
internal sealed class FakeCloudGovernance : ICloudGovernanceRepository
{
    public List<CloudAdminAuditEntry> Audit { get; } = [];

    public Task AddAuditAsync(CloudAdminAuditEntry entry, CancellationToken ct)
    {
        Audit.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CloudItemRule>> ListRulesAsync(Guid vaultId, CancellationToken ct) => throw new NotImplementedException();
    public Task<CloudItemRule?> GetRuleAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task<bool> AddRuleAsync(CloudItemRule rule, CancellationToken ct) => throw new NotImplementedException();
    public Task RemoveRuleAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task RecordSuspectsAsync(Guid vaultId, IReadOnlyList<(string ItemId, string Evidence, long Attempts)> items, DateTimeOffset now, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CloudSuspectItem>> ListSuspectsAsync(Guid vaultId, CloudSuspectStatus? status, CancellationToken ct) => throw new NotImplementedException();
    public Task<CloudSuspectItem?> GetSuspectAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateSuspectAsync(CloudSuspectItem suspect, CancellationToken ct) => throw new NotImplementedException();
    public Task AddDoubtfulAsync(IReadOnlyList<CloudDoubtfulOperation> operations, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CloudDoubtfulOperation>> ListDoubtfulAsync(Guid vaultId, bool openOnly, CancellationToken ct) => throw new NotImplementedException();
    public Task<CloudDoubtfulOperation?> GetDoubtfulAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateDoubtfulAsync(CloudDoubtfulOperation operation, CancellationToken ct) => throw new NotImplementedException();
    public Task<CloudRollbackIncident?> GetOpenIncidentAsync(Guid serverId, CancellationToken ct) => throw new NotImplementedException();
    public Task AddIncidentAsync(CloudRollbackIncident incident, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CloudRollbackIncident>> ListIncidentsAsync(Guid vaultId, bool openOnly, CancellationToken ct) => throw new NotImplementedException();
    public Task<CloudRollbackIncident?> GetIncidentAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateIncidentAsync(CloudRollbackIncident incident, CancellationToken ct) => throw new NotImplementedException();
    public Task<Guid?> PreviousWorldAsync(Guid serverId, Guid currentCredentialId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyDictionary<string, (long Epoch, long Seq)>> LastAppliedByServerAsync(Guid vaultId, Guid serverId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CloudQuarantineView>> ListQuarantineAsync(Guid vaultId, bool openOnly, CancellationToken ct) => throw new NotImplementedException();
    public Task<CloudQuarantineView?> GetQuarantineAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateQuarantineAsync(CloudQuarantine quarantine, CloudBatch batch, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyDictionary<Guid, (string ItemId, string DisplayName)>> ItemNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CloudAdminAuditEntry>> ListAuditAsync(Guid vaultId, int limit, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<CloudLedgerView>> ListLedgerAsync(Guid vaultId, string? playerUuid, int limit, CancellationToken ct) => throw new NotImplementedException();
}
