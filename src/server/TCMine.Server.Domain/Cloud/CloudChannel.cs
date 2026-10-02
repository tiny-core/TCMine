using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Um canal de armazenamento de um jogador numa nuvem. O jogador é
///     identificado pelo UUID do Minecraft (o servidor de jogo recebe da Mojang,
///     em online-mode) — não pelo usuário do TCMine, que pode nem existir ainda
///     para quem só entrou no jogo.
/// </summary>
public sealed class CloudChannel : Entity
{
    public const int NameMaxLength = 48;
    public const string DefaultName = "Principal";

    public required Guid VaultId { get; init; }

    /// <summary>UUID do Minecraft sem hífens, minúsculo (mesmo formato de <c>User.MinecraftUuid</c>).</summary>
    public required string PlayerUuid { get; init; }

    public required string Name { get; set; }

    public CloudChannelStatus Status { get; private set; } = CloudChannelStatus.Active;

    public string? FrozenReason { get; private set; }

    public bool IsFrozen => Status == CloudChannelStatus.Frozen;

    public void Rename(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
            throw new ArgumentException($"Nome do canal entre 1 e {NameMaxLength} caracteres.", nameof(name));
        Name = trimmed;
        Touch();
    }

    /// <summary>
    ///     Congela: nada entra nem sai até o admin descongelar. É o que acontece
    ///     sozinho quando um lote vai para a quarentena.
    /// </summary>
    public void Freeze(string reason)
    {
        Status = CloudChannelStatus.Frozen;
        FrozenReason = reason.Length > 256 ? reason[..256] : reason;
        Touch();
    }

    public void Unfreeze()
    {
        Status = CloudChannelStatus.Active;
        FrozenReason = null;
        Touch();
    }

    /// <summary>Normaliza o UUID do jogador: sem hífens, minúsculo, 32 hex. Nulo se não for UUID.</summary>
    public static string? NormalizePlayerUuid(string? raw)
    {
        if (raw is null) return null;
        var compact = raw.Replace("-", "").Trim().ToLowerInvariant();
        return compact.Length == 32 && compact.All(Uri.IsHexDigit) ? compact : null;
    }
}

public enum CloudChannelStatus
{
    Active,
    Frozen
}
