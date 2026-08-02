namespace TCMine.Contracts.Handshake;

/// <summary>
///     Resposta de <see cref="Protocol.HandshakeRoute" />.
///     FORMATO CONGELADO: nunca remova nem renomeie campo aqui, apenas adicione
///     opcionais. Se este contrato quebrar, um launcher antigo não consegue nem
///     exibir a mensagem dizendo que está desatualizado.
/// </summary>
public sealed record HandshakeResponse
{
    public required int ProtocolMin { get; init; }
    public required int ProtocolMax { get; init; }

    /// <summary>Versão de produto do server. Informativa — não use para gating.</summary>
    public required string ServerVersion { get; init; }

    public required string ServerName { get; init; }

    /// <summary>
    ///     Canal Velopack que este server oferece, ex: "win-x64-p1".
    ///     O canal deriva do PROTOCOLO, não da versão. É por isso que trocar a cor
    ///     de um botão no launcher chega em todos os clientes sem release do server.
    /// </summary>
    public required string LauncherChannel { get; init; }

    /// <summary>Feed de update. O launcher nunca consulta o GitHub direto.</summary>
    public required Uri LauncherFeedUrl { get; init; }

    /// <summary>
    ///     Freio de emergência: força update mesmo dentro do mesmo protocolo,
    ///     para bug crítico ou falha de segurança. Use com parcimônia.
    /// </summary>
    public string? MinLauncherVersion { get; init; }

    /// <summary>Congela clientes na versão atual (útil durante um evento).</summary>
    public bool UpdatesFrozen { get; init; }

    public required IReadOnlyList<string> Capabilities { get; init; }

    public required string AzureClientId { get; init; }
}

public enum HandshakeOutcome
{
    Ok,
    LauncherTooOld,
    LauncherTooNew,
    Unreachable,
    InvalidResponse
}
