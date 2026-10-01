using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TCMine.Server.Web.Tests.Infrastructure;

/// <summary>
///     Cria o admin da instalação (ou loga quem já existe) e devolve o cookie
///     de sessão. Vai pelo par real /auth/microsoft/start → /auth/microsoft/callback
///     de propósito: forjar o cookie à mão testaria o formato que o teste
///     inventou, não o que o <c>SignInAsync</c> emite. O único trecho que não é
///     real é a conversa com a própria Microsoft — <see cref="FakeMicrosoftOAuthClient" />,
///     registrado por padrão em <see cref="TcMineAppFactory" />, devolve uma
///     identidade derivada do "code" sem sair para a rede.
/// </summary>
internal static class AutenticacaoDeTeste
{
    public static Task<string> EntrarComoAdminAsync(this TcMineAppFactory factory) =>
        factory.EntrarPelaMicrosoftAsync("codigo-admin");

    /// <summary>
    ///     Mesmo "code" sempre volta como o mesmo usuário Microsoft — é assim que
    ///     um teste simula duas contas diferentes (dois codes) ou a mesma pessoa
    ///     voltando (o mesmo code de novo).
    /// </summary>
    public static async Task<string> EntrarPelaMicrosoftAsync(this TcMineAppFactory factory, string code)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var start = await client.GetAsync("/auth/microsoft/start");
        var state = ExtrairState(ExtrairLocation(start));
        var oauthCookie = ExtrairCookie(start, "tcmine.oauth=")
                          ?? throw new InvalidOperationException("/start não emitiu o cookie de PKCE.");

        using var callbackRequest =
            new HttpRequestMessage(HttpMethod.Get, $"/auth/microsoft/callback?code={code}&state={state}");
        callbackRequest.Headers.Add("Cookie", oauthCookie);

        var callback = await client.SendAsync(callbackRequest);

        if (callback.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found))
            throw new InvalidOperationException($"Login falhou: {callback.StatusCode}");

        return ExtrairCookie(callback, "tcmine.auth=")
               ?? throw new InvalidOperationException("Login não emitiu cookie de sessão.");
    }

    private static string ExtrairLocation(HttpResponseMessage response) =>
        response.Headers.Location?.ToString()
        ?? throw new InvalidOperationException("Resposta sem Location — esperava um redirect.");

    private static string ExtrairState(string location)
    {
        var match = Regex.Match(location, "[?&]state=([^&]+)");

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("/start não incluiu state na URL de autorização.");
    }

    private static string? ExtrairCookie(HttpResponseMessage response, string prefixo)
    {
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.FirstOrDefault(v => v.StartsWith(prefixo, StringComparison.Ordinal))
            : null;

        // Só o par nome=valor interessa ao cabeçalho Cookie; o resto são
        // diretivas de armazenamento que só o browser consome.
        return setCookie?.Split(';')[0];
    }
}
