namespace TCMine.Server.Application.Cloud;

// Formato das mensagens da API do mod (/api/cloud/v1). São os próprios corpos
// JSON (camelCase): o cliente é o mod em Java, não o launcher, então não moram
// em TCMine.Contracts. Mudança quebrada aqui = subir CloudProtocol.Current.

/// <summary>Versão do protocolo da API do mod. Mod com outra versão recebe 426 no hello.</summary>
public static class CloudProtocol
{
    public const int Current = 1;

    /// <summary>Teto de operações e de definições de item por lote (protege contra corpo gigante).</summary>
    public const int MaxOpsPerBatch = 500;

    /// <summary>Teto de leases por heartbeat.</summary>
    public const int MaxLeasesPerHeartbeat = 500;
}

/// <summary>Quem está falando: resolvido pela chave, NUNCA por um campo do corpo.</summary>
public sealed record CloudServerContext(Guid ServerId, Guid VaultId, Guid CredentialId);

public sealed record CloudSeqPosition(long Epoch, long Seq);

public sealed record CloudCheckpoint(Guid WorldId, IReadOnlyDictionary<string, CloudSeqPosition>? Players);

public sealed record CloudHelloRequest(int Protocol, string? ModVersion, CloudCheckpoint Checkpoint);

public sealed record CloudQuotaDto(int MaxTypes, long MaxTotal);

public sealed record CloudHelloReply(
    int Protocol,
    string PolicyMode,
    long PolicyVersion,
    CloudQuotaDto Quota,
    int MaxItemBytes,
    int MaxChannelsPerPlayer,
    int LeaseTtlSeconds,
    bool ReadOnly,
    string? ReadOnlyReason);

public sealed record CloudAcquireRequest(string PlayerUuid, string? PlayerName);

public sealed record CloudItemDto(string Fingerprint, string ItemId, string DisplayName, string Encoded);

public sealed record CloudChannelItemDto(string Fingerprint, long Amount, string ItemId, string DisplayName, string Encoded);

public sealed record CloudChannelDto(Guid Id, string Name, bool Frozen, IReadOnlyList<CloudChannelItemDto> Items);

/// <summary>Resposta do acquire: <c>Status</c> "granted" (com época e canais) ou "busy" (com quem está).</summary>
public sealed record CloudAcquireReply(
    string Status,
    long Epoch,
    bool ReadOnly,
    IReadOnlyList<CloudChannelDto> Channels,
    string? Holder);

public sealed record CloudHeldLeaseDto(string PlayerUuid, long Epoch);

public sealed record CloudHeartbeatRequest(IReadOnlyList<CloudHeldLeaseDto> Leases);

/// <summary><c>Lost</c>: jogadores cujo lease este servidor já não segura (o mod trava o canal).</summary>
public sealed record CloudHeartbeatReply(IReadOnlyList<string> Lost);

public sealed record CloudOpDto(Guid ChannelId, string Fingerprint, long Delta);

public sealed record CloudExpectedDto(Guid ChannelId, string Fingerprint, long Amount);

public sealed record CloudBatchRequest(
    string PlayerUuid,
    long Epoch,
    long Seq,
    IReadOnlyList<CloudOpDto> Ops,
    IReadOnlyList<CloudExpectedDto> Expected,
    IReadOnlyList<CloudItemDto>? Definitions);

/// <summary><c>Result</c>: "applied", "duplicate" ou "quarantined" (com o motivo).</summary>
public sealed record CloudBatchReply(string Result, string? Reason);

public sealed record CloudReleaseRequest(string PlayerUuid, long Epoch, long LastSeq);

public sealed record CloudReleaseReply(bool Released);

public sealed record CloudDoubtfulDto(string PlayerUuid, Guid ChannelId, string Fingerprint, string Kind, long Amount);

public sealed record CloudDoubtfulRequest(IReadOnlyList<CloudDoubtfulDto> Operations);
