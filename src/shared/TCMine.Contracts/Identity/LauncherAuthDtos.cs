namespace TCMine.Contracts.Identity;

/// <summary>
///     Login do launcher. O token é o access token dos serviços do Minecraft,
///     obtido pela cadeia Microsoft → Xbox Live → XSTS na máquina do jogador.
///     Ele NÃO é guardado pelo servidor: serve uma vez, para provar a identidade,
///     e a sessão que vale a partir daí é o cookie devolvido na resposta.
/// </summary>
public sealed record MinecraftLoginRequest
{
    public required string AccessToken { get; init; }
}

/// <summary>
///     Resgate de convite. O código é o que o jogador recebeu de quem o
///     convidou, no formato XXXX-XXXX-XXXX-XXXX — hífens e caixa são cosméticos
///     e o servidor normaliza antes de comparar.
/// </summary>
public sealed record RedeemInviteRequest
{
    public required string Code { get; init; }
}

/// <summary>
///     Quem o servidor reconheceu. O launcher usa isto para exibir o jogador na
///     interface sem refazer a cadeia de autenticação a cada abertura.
/// </summary>
public sealed record LauncherSessionDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>UUID da conta Minecraft, minúsculo e sem hífens.</summary>
    public required string MinecraftUuid { get; init; }
}
