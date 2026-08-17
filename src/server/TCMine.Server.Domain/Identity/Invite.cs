using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Identity;

/// <summary>
///     Convite para um servidor, com o papel que ele concede.
///     É de uso único e nominal ao resgate: um convite vira exatamente um
///     <see cref="Membership" />. Convite reutilizável pareceria conveniente
///     ("mando o link no grupo"), mas um código de Admin vazado no Discord vira
///     acesso permanente para quem passar por ali — e não haveria como saber
///     quantas pessoas já entraram por ele.
/// </summary>
public sealed class Invite : Entity
{
    /// <summary>
    ///     Hash do código, nunca o código. O valor em claro é exibido uma única
    ///     vez, na criação: um banco vazado não pode entregar convites de Owner
    ///     prontos para usar. O custo é que quem perdeu o código gera outro.
    /// </summary>
    public required string CodeHash { get; init; }

    public required Guid GameServerId { get; init; }

    public required ServerRole Role { get; init; }

    /// <summary>Quem convidou. Fica no registro para haver a quem perguntar depois.</summary>
    public required Guid CreatedByUserId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public Guid? RedeemedByUserId { get; private set; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    ///     Estado derivado das três datas, e não de uma coluna de status: um
    ///     enum separado poderia discordar delas, e o convite expirado nunca
    ///     receberia a escrita que o marcaria como tal.
    /// </summary>
    public bool IsUsable(DateTimeOffset now) =>
        RedeemedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Redeem(Guid userId, DateTimeOffset now)
    {
        if (!IsUsable(now))
            throw new InvalidOperationException("Convite não está mais disponível.");

        RedeemedByUserId = userId;
        RedeemedAt = now;
        Touch();
    }

    /// <summary>
    ///     Cancela um convite ainda não usado. Idempotente de propósito: dois
    ///     cliques em "revogar" não devem produzir erro, e o que importa é o
    ///     convite deixar de servir.
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        if (RedeemedAt is not null)
            throw new InvalidOperationException("Convite já foi resgatado; remova o membro.");

        RevokedAt ??= now;
        Touch();
    }
}
