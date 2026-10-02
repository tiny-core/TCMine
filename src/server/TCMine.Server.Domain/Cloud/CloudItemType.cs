using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Um tipo de item que já passou pela nuvem, endereçado pela impressão
///     digital (SHA-256 do NBT canônico, calculada pelo mod) — a mesma ideia do
///     blob store: conteúdo idêntico, linha única, compartilhada entre nuvens.
///     O TCMine NUNCA decodifica <see cref="Encoded" />: guarda e devolve. Id e
///     nome existem só para o painel mostrar o item sem o Minecraft.
///     Imutável depois de criado.
/// </summary>
public sealed class CloudItemType : Entity
{
    public const int FingerprintLength = 64;

    public required string Fingerprint { get; init; }

    /// <summary><c>mod:item</c>, como o servidor de jogo mandou.</summary>
    public required string ItemId { get; init; }

    public required string ModId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>NBT do item com quantidade 1, opaco para o TCMine.</summary>
    public required byte[] Encoded { get; init; }
}
