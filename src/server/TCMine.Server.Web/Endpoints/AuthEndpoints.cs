using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     Login, logout e setup inicial.
///     São endpoints HTTP (e não componentes interativos) porque gravar o cookie
///     de sessão exige um HttpContext vivo — dentro do circuito SignalR do Blazor
///     os headers da resposta já foram enviados.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        // Grupo em vez de repetir os dois modificadores em cada rota: aqui o
        // esquecimento seria caro nas duas pontas — uma rota sem AllowAnonymous
        // exigiria sessão para poder entrar, e uma sem limite de taxa seria a
        // porta de força bruta que as outras três fecharam.
        var anonimas = app
            .MapGroup("/auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.AuthPolicy);

        anonimas.MapPost("/login", async (
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string? returnUrl,
            AuthenticateUser useCase,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await useCase.HandleAsync(email, password, ct);
            if (!result.Succeeded)
            {
                var back = BuildUrl("/login", result.Error!, returnUrl);
                return Results.Redirect(back);
            }

            await SignInAsync(http, result.Value!);

            // Só aceita destino local: um returnUrl absoluto viraria open
            // redirect (phishing com domínio legítimo no link).
            return Results.LocalRedirect(SafeReturnUrl(returnUrl));
        });

        anonimas.MapPost("/setup", async (
            [FromForm] string email,
            [FromForm] string displayName,
            [FromForm] string password,
            CreateFirstAdmin useCase,
            IUserRepository users,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await useCase.HandleAsync(email, displayName, password, ct);
            if (!result.Succeeded)
                return Results.Redirect(BuildUrl("/setup", result.Error!, null));

            // Já entra logado: acabou de provar que é dono da instalação.
            var created = await users.GetByIdAsync(result.Value, ct);
            if (created is not null)
                await SignInAsync(http, created);

            return Results.LocalRedirect("/");
        });

        anonimas.MapPost("/forgot-password", async (
            [FromForm] string email,
            RequestPasswordReset useCase,
            HttpContext http,
            CancellationToken ct) =>
        {
            // O link precisa do endereço público do painel; monta a partir da
            // requisição atual para funcionar em qualquer host/porta.
            var template = $"{http.Request.Scheme}://{http.Request.Host}/reset-password?token={{token}}";
            await useCase.HandleAsync(email, template, ct);

            // Resposta idêntica exista ou não a conta — ver o caso de uso.
            return Results.LocalRedirect("/forgot-password?sent=true");
        });

        anonimas.MapPost("/reset-password", async (
            [FromForm] string email,
            [FromForm] string token,
            [FromForm] string password,
            ResetPassword useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.HandleAsync(email, token, password, ct);
            if (!result.Succeeded)
            {
                var back = $"/reset-password?token={Uri.EscapeDataString(token)}"
                           + $"&error={Uri.EscapeDataString(result.Error!)}";
                return Results.Redirect(back);
            }

            // Não entra logado de propósito: quem redefiniu prova a posse da
            // senha nova entrando com ela.
            return Results.LocalRedirect("/login");
        });

        // Fora do grupo: sair exige sessão, e limitar quem já está autenticado
        // só atrapalharia.
        app.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login");
        });

        return app;
    }

    /// <summary>
    ///     Grava o cookie de sessão. Interno porque o login do launcher
    ///     (<see cref="LauncherAuthEndpoints" />) emite exatamente a mesma sessão:
    ///     duas montagens de claims divergiriam, e a que esquecesse
    ///     <c>IsInstanceAdmin</c> criaria um caminho de login que silenciosamente
    ///     rebaixa quem entra por ele.
    /// </summary>
    internal static async Task SignInAsync(HttpContext http, User user)
    {
        // As claims são a fonte de verdade da sessão. IsInstanceAdmin entra como
        // claim para o middleware autorizar sem ir ao banco a cada requisição;
        // papéis por servidor continuam sendo consultados ao vivo (podem mudar).
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName)
        ];

        // Conta que entrou pelo launcher não tem e-mail; uma Claim com valor
        // nulo lança na construção, então a ausência é omissão, não string vazia.
        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        if (user.IsInstanceAdmin)
            claims.Add(new Claim(TcMineClaims.InstanceAdmin, "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static string BuildUrl(string path, string error, string? returnUrl)
    {
        var url = $"{path}?error={Uri.EscapeDataString(error)}";
        return string.IsNullOrEmpty(returnUrl)
            ? url
            : $"{url}&returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    // Aceita só caminho relativo dentro do app; qualquer outra coisa vira "/".
    private static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        var candidate = returnUrl.StartsWith('/') ? returnUrl : "/" + returnUrl;

        // "//host" e "/\host" são absolutos disfarçados.
        return candidate.StartsWith("//", StringComparison.Ordinal)
               || candidate.StartsWith("/\\", StringComparison.Ordinal)
            ? "/"
            : candidate;
    }
}

public static class TcMineClaims
{
    public const string InstanceAdmin = "tcmine:instance-admin";
}
