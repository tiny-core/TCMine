using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     Login, logout e setup inicial — tudo pela conta Microsoft, não há mais
///     login local.
///     São endpoints HTTP (e não componentes interativos) porque gravar o cookie
///     de sessão exige um HttpContext vivo — dentro do circuito SignalR do Blazor
///     os headers da resposta já foram enviados.
/// </summary>
public static class AuthEndpoints
{
    // Cookie efêmero que carrega o PKCE (code_verifier) e o anti-CSRF (state)
    // entre o /start e o /callback — o navegador vai até a Microsoft e volta, e
    // sem um lugar para guardar isso no meio do caminho não haveria como provar
    // depois que a resposta é do mesmo pedido que a iniciou. Path restrito aos
    // dois endpoints: não há motivo para ele viajar em nenhuma outra requisição.
    private const string OAuthCookieName = "tcmine.oauth";
    private const string OAuthCookiePath = "/auth/microsoft";

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        // Grupo em vez de repetir os dois modificadores em cada rota: aqui o
        // esquecimento seria caro nas duas pontas — uma rota sem AllowAnonymous
        // exigiria sessão para poder entrar, e uma sem limite de taxa seria a
        // porta de força bruta que as outras fecharam.
        var anonimas = app
            .MapGroup("/auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.AuthPolicy);

        anonimas.MapGet("/microsoft/start", async (
            HttpContext http,
            ISettingsRepository settings,
            IOptions<ServerOptions> options,
            IHostEnvironment env,
            [FromQuery] string? returnUrl,
            [FromQuery] bool? link,
            CancellationToken ct) =>
        {
            var clientId = await AzureClientIdResolver.ResolveAsync(settings, options.Value.AzureClientId, ct);

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.Redirect(
                    BuildUrl("/admin/login", "Configure o ID do aplicativo Azure antes de entrar.", null));
            }

            // "Vincular" só faz sentido com sessão já aberta — sem isso, cai no
            // caminho normal de login (entrar com a Microsoft, de novo).
            var isLink = link == true && http.User.Identity?.IsAuthenticated == true;

            var verifier = SecureToken.Generate();
            var state = SecureToken.Generate();
            var redirectUri = CallbackUrl(http, options.Value.PublicUrl, env);

            http.Response.Cookies.Append(OAuthCookieName, EncodeOAuthCookie(state, verifier, isLink, returnUrl),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = http.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10),
                    Path = OAuthCookiePath
                });

            var authorize = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize"
                            + $"?client_id={Uri.EscapeDataString(clientId)}"
                            + "&response_type=code"
                            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                            + "&response_mode=query"
                            + "&scope=" + Uri.EscapeDataString("openid profile XboxLive.signin offline_access")
                            + $"&state={state}"
                            + $"&code_challenge={ComputeCodeChallenge(verifier)}"
                            + "&code_challenge_method=S256";

            return Results.Redirect(authorize);
        });

        anonimas.MapGet("/microsoft/callback", async (
            HttpContext http,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery(Name = "error_description")]
            string? errorDescription,
            AuthenticateMicrosoftUser authenticate,
            LinkMinecraftAccount linkMinecraft,
            IMicrosoftOAuthClient oauth,
            IUserRepository users,
            ISettingsRepository settings,
            IOptions<ServerOptions> options,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            var cookie = http.Request.Cookies[OAuthCookieName];
            http.Response.Cookies.Delete(OAuthCookieName, new CookieOptions { Path = OAuthCookiePath });

            if (!TryDecodeOAuthCookie(cookie, out var expectedState, out var verifier, out var isLink,
                    out var returnUrl))
                return Results.Redirect(BuildUrl("/admin/login", "Sessão de login expirou. Tente de novo.", null));

            if (error is not null)
            {
                var message = error == "access_denied"
                    ? "Login cancelado."
                    : $"A Microsoft recusou o login: {errorDescription ?? error}";

                return Results.Redirect(BuildUrl(isLink ? "/admin" : "/admin/login", message, null));
            }

            if (string.IsNullOrEmpty(code) || state != expectedState)
            {
                return Results.Redirect(
                    BuildUrl("/admin/login", "Resposta inválida da Microsoft. Tente de novo.", null));
            }

            var clientId = await AzureClientIdResolver.ResolveAsync(settings, options.Value.AzureClientId, ct);
            var redirectUri = CallbackUrl(http, options.Value.PublicUrl, env);

            // Vincular: a sessão já existe, só falta o Minecraft. Troca o código
            // direto pelo token Microsoft — sem passar por AuthenticateMicrosoftUser,
            // que resolveria (ou criaria) uma CONTA, não é o que "vincular" pede.
            if (isLink && http.User.Identity?.IsAuthenticated == true)
            {
                var microsoftIdentity = await oauth.ExchangeCodeAsync(clientId, code, redirectUri, verifier, ct);

                if (!microsoftIdentity.Succeeded)
                    return Results.Redirect(BuildUrl("/admin", microsoftIdentity.Error!, null));

                var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var linked = await linkMinecraft.HandleAsync(userId, microsoftIdentity.Value!.AccessToken, ct);

                if (!linked.Succeeded)
                    return Results.Redirect(BuildUrl("/admin", linked.Error!, null));

                // Refaz a sessão: HasMinecraft é claim, não consulta ao vivo (ver
                // SignInAsync), e sem reemitir o cookie o menu continuaria
                // oferecendo "vincular" a quem acabou de vincular.
                var reloaded = await users.GetByIdAsync(userId, ct);
                if (reloaded is not null)
                    await SignInAsync(http, reloaded);

                return Results.LocalRedirect("/admin?linked=true");
            }

            var result = await authenticate.HandleAsync(clientId, code, redirectUri, verifier, ct);

            if (!result.Succeeded)
                return Results.Redirect(BuildUrl("/admin/login", result.Error!, null));

            await SignInAsync(http, result.Value!);

            // Só aceita destino local: um returnUrl absoluto viraria open
            // redirect (phishing com domínio legítimo no link).
            return Results.LocalRedirect(SafeReturnUrl(returnUrl));
        });

        anonimas.MapPost("/setup", async (
            [FromForm] string azureClientId,
            IUserRepository users,
            UpdateSettings updateSettings,
            CancellationToken ct) =>
        {
            // Mesma guarda de sempre: só funciona enquanto não existe ninguém.
            if (await users.AnyAsync(ct))
                return Results.Redirect(BuildUrl("/admin/login", "A instalação já tem um administrador.", null));

            var result = await updateSettings.HandleAsync(
                new UpdateSettingsCommand { AzureClientId = azureClientId }, ct);

            if (!result.Succeeded)
                return Results.Redirect(BuildUrl("/admin/setup", result.Error!, null));

            return Results.LocalRedirect("/admin/login");
        });

        // Resgate da administração com o código do log (ver ClaimInstanceAdmin).
        // Exige sessão — o código promove QUEM está logado — e passa pelo mesmo
        // limite de taxa do login: é uma credencial a adivinhar como outra.
        app.MapPost("/auth/claim-admin", async (
                [FromForm] string code,
                HttpContext http,
                ClaimInstanceAdmin claim,
                CancellationToken ct) =>
            {
                var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await claim.HandleAsync(userId, code, ct);

                if (!result.Succeeded)
                    return Results.Redirect(BuildUrl("/admin/claim", result.Error!, null));

                // A sessão carrega IsInstanceAdmin como claim: sem reemitir o
                // cookie, o painel continuaria a tratá-lo como jogador comum.
                await SignInAsync(http, result.Value!);
                return Results.LocalRedirect(SafeReturnUrl(null));
            })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.AuthPolicy);

        // Fora do grupo: sair exige sessão, e limitar quem já está autenticado
        // só atrapalharia.
        app.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/admin/login");
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
        // As claims são a fonte de verdade da sessão. IsInstanceAdmin e
        // HasMinecraft entram como claim para o menu decidir sem ir ao banco a
        // cada requisição; papéis por servidor continuam sendo consultados ao
        // vivo (podem mudar).
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName)
        ];

        if (user.IsInstanceAdmin)
            claims.Add(new Claim(TcMineClaims.InstanceAdmin, "true"));

        if (user.MinecraftUuid is not null)
            claims.Add(new Claim(TcMineClaims.HasMinecraft, "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    /// <summary>
    ///     De onde sai o <c>redirect_uri</c> que a Microsoft exige bater
    ///     EXATAMENTE com o cadastrado no app do Entra ID: do <c>PublicUrl</c>
    ///     configurado, fora de Development — nunca do Scheme/Host da
    ///     requisição em produção. É o mesmo motivo do
    ///     <see cref="HandshakeEndpoints" />: atrás de um proxy reverso (o
    ///     caso comum em produção, Docker incluído) o Host visto aqui pode não
    ///     ser o que o jogador digitou, e o valor calculado nunca bateria com
    ///     o que está registrado — a Microsoft devolve "invalid_request:
    ///     redirect_uri is not valid" sem dizer que a causa é essa.
    ///     EM DEVELOPMENT, ignora o PublicUrl mesmo que ele exista: o
    ///     appsettings.json base traz um valor de template
    ///     ("https://localhost:7001") que não bate com NENHUM dos perfis reais
    ///     do launchSettings.json (5144/7125) — usá-lo aqui mandaria sempre a
    ///     porta errada para quem roda localmente, não importa qual perfil
    ///     escolheu. Em dev não há proxy no meio, então o Scheme/Host da
    ///     própria requisição já é a verdade — mesma exceção que
    ///     OptionsValidation já faz para este campo.
    /// </summary>
    private static string CallbackUrl(HttpContext http, Uri? publicUrl, IHostEnvironment env) =>
        !env.IsDevelopment() && publicUrl is not null
            ? new Uri(publicUrl, "/auth/microsoft/callback").ToString()
            : $"{http.Request.Scheme}://{http.Request.Host}/auth/microsoft/callback";

    /// <summary>
    ///     PKCE S256: SHA-256 do verifier, em base64url sem padding — é assim que
    ///     a Microsoft espera o desafio, e é o verifier em claro (não o hash) que
    ///     volta no /token para ela conferir que bate.
    /// </summary>
    private static string ComputeCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    ///     state e verifier já são base64url (sem "|" nem "="); o returnUrl é o
    ///     único campo que precisa de escape antes de entrar no valor do cookie.
    /// </summary>
    private static string EncodeOAuthCookie(string state, string verifier, bool isLink, string? returnUrl) =>
        string.Join('|', state, verifier, isLink ? "1" : "0", Uri.EscapeDataString(returnUrl ?? ""));

    private static bool TryDecodeOAuthCookie(
        string? cookie, out string state, out string verifier, out bool isLink, out string? returnUrl)
    {
        (state, verifier, returnUrl) = ("", "", null);
        isLink = false;

        if (string.IsNullOrEmpty(cookie))
            return false;

        var parts = cookie.Split('|');
        if (parts.Length != 4)
            return false;

        state = parts[0];
        verifier = parts[1];
        isLink = parts[2] == "1";
        returnUrl = Uri.UnescapeDataString(parts[3]) is { Length: > 0 } value ? value : null;

        return state.Length > 0 && verifier.Length > 0;
    }

    private static string BuildUrl(string path, string error, string? returnUrl)
    {
        var url = $"{path}?error={Uri.EscapeDataString(error)}";
        return string.IsNullOrEmpty(returnUrl)
            ? url
            : $"{url}&returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    // Aceita só caminho relativo dentro do app; qualquer outra coisa vira o
    // painel — "/" agora é a página pública, não o destino de quem loga.
    private static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/admin";

        var candidate = returnUrl.StartsWith('/') ? returnUrl : "/" + returnUrl;

        // "//host" e "/\host" são absolutos disfarçados.
        return candidate.StartsWith("//", StringComparison.Ordinal)
               || candidate.StartsWith("/\\", StringComparison.Ordinal)
            ? "/admin"
            : candidate;
    }
}

public static class TcMineClaims
{
    public const string InstanceAdmin = "tcmine:instance-admin";
    public const string HasMinecraft = "tcmine:has-minecraft";
}
