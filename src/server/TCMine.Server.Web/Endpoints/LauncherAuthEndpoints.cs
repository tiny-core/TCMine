using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TCMine.Contracts.Identity;
using TCMine.Server.Application.Security;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     Login do jogador pelo launcher.
///     Separado do <see cref="AuthEndpoints" /> porque o formato é outro: aqui
///     não há formulário nem redirecionamento, e sim JSON e códigos de status —
///     um cliente desktop não tem para onde ser redirecionado. A sessão emitida,
///     porém, é exatamente a mesma do painel, e é isso que faz o hub, os blobs e
///     o ICurrentUserScope funcionarem sem nenhum caminho paralelo.
/// </summary>
public static class LauncherAuthEndpoints
{
    public static IEndpointRouteBuilder MapLauncherAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/minecraft", async (
                [FromBody] MinecraftLoginRequest request,
                AuthenticateMinecraftUser useCase,
                HttpContext http,
                CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(request.AccessToken, ct);

                // 401 e não 400: o pedido está bem formado, o que falhou foi a
                // credencial. O launcher distingue os dois para saber se refaz a
                // cadeia da Microsoft ou se avisa que a conta não serve.
                if (!result.Succeeded)
                    return Results.Problem(result.Error, statusCode: StatusCodes.Status401Unauthorized);

                var user = result.Value!;
                await AuthEndpoints.SignInAsync(http, user);

                return Results.Ok(new LauncherSessionDto
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,

                    // Não pode ser nulo aqui: o caso de uso só devolve sucesso
                    // depois de a Mojang confirmar o perfil.
                    MinecraftUuid = user.MinecraftUuid!
                });
            })
            .WithName("LoginMinecraft")
            .AllowAnonymous()
            // Mesmo teto do login local: é uma porta de autenticação, e a única
            // diferença é quem valida a credencial do outro lado.
            .RequireRateLimiting(RateLimitPolicies.AuthPolicy);

        // Contraparte do /auth/logout do painel, em JSON: o launcher precisa
        // conseguir descartar a sessão sem seguir um redirecionamento.
        app.MapPost("/api/v1/auth/logout", async (HttpContext http) =>
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            })
            .WithName("LogoutLauncher")
            .RequireAuthorization();

        return app;
    }
}
