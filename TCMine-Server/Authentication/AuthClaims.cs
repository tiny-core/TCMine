using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using TCMine_Domain.Entities;

namespace TCMine_Server.Authentication;

/// <summary>Monta o <see cref="ClaimsPrincipal" /> de um usuário para o cookie de autenticação.</summary>
public static class AuthClaims
{
    public static ClaimsPrincipal BuildPrincipal(UserEntity user)
    {
        var identity = new ClaimsIdentity(
            CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        return new ClaimsPrincipal(identity);
    }
}