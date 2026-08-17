using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Web.Endpoints;

namespace TCMine.Server.Web.Security;

/// <summary>
///     Quem está fazendo a requisição, a partir das claims da sessão.
///     Substitui o stub de desenvolvimento: aqui o UserId vem do cookie
///     assinado, não de uma constante.
///     O principal vem do <see cref="UserPrincipalHolder" /> e não direto do
///     <c>IHttpContextAccessor</c>: fora de uma requisição HTTP em curso — numa
///     invocação de hub, por exemplo — o accessor responde conforme o
///     transporte, e a identidade some sem erro nenhum.
/// </summary>
public sealed class HttpContextUserScope(
    UserPrincipalHolder holder,
    IDbContextFactory<TcMineDbContext> factory) : ICurrentUserScope
{
    private ClaimsPrincipal? Principal => holder.Current;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    ///     Dono dos recursos criados nesta sessão. Anônimo (handshake, download
    ///     público) não cria nada, então Guid.Empty nunca vira dono de verdade.
    /// </summary>
    public Guid OwnerId => UserId ?? Guid.Empty;

    public bool IsInstanceAdmin =>
        Principal?.HasClaim(TcMineClaims.InstanceAdmin, "true") is true;

    public async Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct)
    {
        if (UserId is not { } userId)
            return null;

        // Admin da instalação manda em qualquer servidor.
        if (IsInstanceAdmin)
            return ServerRoleDto.Owner;

        // Consulta ao vivo de propósito: se o admin rebaixa alguém, vale já na
        // próxima chamada — não na próxima sessão (contrato do ICurrentUserScope).
        await using var db = await factory.CreateDbContextAsync(ct);
        var membership = await db.Memberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.GameServerId == gameServerId, ct);

        // Mapeamento explícito: os dois enums têm os mesmos valores hoje, mas um
        // cast silencioso viraria bug no dia em que um deles ganhasse um papel.
        return membership?.Role switch
        {
            ServerRole.Member => ServerRoleDto.Member,
            ServerRole.Moderator => ServerRoleDto.Moderator,
            ServerRole.Admin => ServerRoleDto.Admin,
            ServerRole.Owner => ServerRoleDto.Owner,
            _ => null
        };
    }
}
