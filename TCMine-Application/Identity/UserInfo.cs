using System.Security.Claims;

namespace TCMine_Application.Identity;

/// <summary>Forma serializável da identidade persistida entre prerender e circuito.</summary>
public sealed record UserInfo(string Name, string Role)
{
    public static UserInfo FromPrincipal(ClaimsPrincipal principal)
    {
        return new UserInfo(
            principal.Identity?.Name ?? string.Empty,
            principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty);
    }

    public ClaimsPrincipal ToPrincipal()
    {
        // AuthenticationType não-vazio é o que torna a identidade "autenticada"
        var identity = new ClaimsIdentity("TCMine", ClaimTypes.Name, ClaimTypes.Role);
        if (!string.IsNullOrEmpty(Name)) identity.AddClaim(new Claim(ClaimTypes.Name, Name));
        if (!string.IsNullOrEmpty(Role)) identity.AddClaim(new Claim(ClaimTypes.Role, Role));
        return new ClaimsPrincipal(identity);
    }
}