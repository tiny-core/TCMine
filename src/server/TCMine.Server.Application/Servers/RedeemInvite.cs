using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Resgata um convite e cria o vínculo com o servidor.
///     Único caso de uso de servidor que não consulta papel — é ele que CONCEDE
///     o papel. O que faz as vezes de autorização aqui é a posse do código: quem
///     o apresenta prova que foi convidado.
/// </summary>
public sealed class RedeemInvite(
    IInviteRepository invites,
    IMembershipRepository memberships,
    IServerWhitelistSync whitelist,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(string code, CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return Result.Fail("Entre na sua conta antes de usar um convite.");

        if (string.IsNullOrWhiteSpace(code))
            return Result.Fail("Informe o código do convite.");

        var invite = await invites.GetByCodeHashAsync(
            SecureToken.Hash(SecureToken.NormalizeCode(code)), ct);

        var agora = DateTimeOffset.UtcNow;

        // Mensagem única para inexistente, expirado, revogado e já usado.
        // Distinguir permitiria varrer códigos e descobrir quais existem — e
        // saber que um código existe mas expirou já é meio caminho.
        if (invite is null || !invite.IsUsable(agora))
            return Result.Fail("Convite inválido ou expirado.");

        var existente = await memberships.GetAsync(userId, invite.GameServerId, ct);

        if (existente is not null)
        {
            // Já é membro. O convite promove, mas nunca rebaixa: usar um convite
            // de Member sem querer não pode custar o papel de Admin de alguém.
            if (existente.Role < invite.Role)
            {
                existente.Role = invite.Role;
                await memberships.UpdateAsync(existente, ct);
            }
        }
        else
        {
            await memberships.AddAsync(
                new Membership
                {
                    UserId = userId,
                    GameServerId = invite.GameServerId,
                    Role = invite.Role
                },
                ct);
        }

        // Marcado depois de o vínculo existir: se a ordem fosse a inversa e a
        // criação falhasse, o convite ficaria queimado sem ter concedido nada.
        invite.Redeem(userId, agora);
        await invites.UpdateAsync(invite, ct);

        // Sem isto o convite dá acesso ao painel e não ao JOGO: o jogador entra,
        // vê o servidor na lista, e leva "not white-listed" na conexão. A
        // sincronização é silenciosa se o servidor estiver parado — a próxima
        // subida a refaz.
        await whitelist.HandleAsync(invite.GameServerId, ct);

        return Result.Success();
    }
}
