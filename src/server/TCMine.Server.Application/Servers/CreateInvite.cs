using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Gera um convite para um servidor.
///     Devolve o código em CLARO, e é a única vez que ele existe fora da cabeça
///     de quem convidou — o banco guarda só o hash.
/// </summary>
public sealed class CreateInvite(
    IInviteRepository invites,
    ICurrentUserScope scope)
{
    /// <summary>
    ///     Validade padrão. Curta de propósito: convite é para ser usado agora,
    ///     e um código que vale para sempre acaba colado num histórico de chat
    ///     que alguém lê meses depois.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    public async Task<Result<string>> HandleAsync(
        Guid gameServerId,
        ServerRoleDto role,
        CancellationToken ct,
        TimeSpan? lifetime = null)
    {
        var auth = await scope.RequireAsync(gameServerId, ServerAccessPolicy.CanManageMembers, ct);
        if (!auth.Succeeded)
            return Result<string>.Fail(auth.Error!);

        if (scope.UserId is not { } convidou)
            return Result<string>.Fail("Sessão expirada. Entre de novo.");

        // Convidar alguém como Owner daria a essa pessoa o poder de remover quem
        // a convidou. Owner se transfere deliberadamente, não por link.
        if (role >= ServerRoleDto.Owner)
            return Result<string>.Fail("Convite não pode conceder o papel de dono.");

        var code = SecureToken.GenerateCode();

        await invites.AddAsync(
            new Invite
            {
                CodeHash = SecureToken.Hash(SecureToken.NormalizeCode(code)),
                GameServerId = gameServerId,
                Role = role.ToDomain(),
                CreatedByUserId = convidou,
                ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? DefaultLifetime)
            },
            ct);

        return Result<string>.Success(code);
    }
}
