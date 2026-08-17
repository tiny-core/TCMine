using System.Net;
using System.Text.RegularExpressions;

namespace TCMine.Server.Web.Tests.Infrastructure;

/// <summary>
///     Cria o admin da instalação e devolve o cookie de sessão dele.
///     Vai pelo endpoint real de propósito: forjar o cookie à mão testaria o
///     formato que o teste inventou, não o que o <c>SignInAsync</c> emite.
/// </summary>
internal static class AutenticacaoDeTeste
{
    public static async Task<string> EntrarComoAdminAsync(this TcMineAppFactory factory)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // O POST de verdade carrega o token de antiforgery, então o teste tem de
        // buscar a página antes: pular essa etapa testaria um caminho que o
        // pipeline real rejeita.
        var token = ExtrairToken(await client.GetStringAsync("/setup"));

        var resposta = await client.PostAsync("/auth/setup", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("email", "admin@tcmine.test"),
            new KeyValuePair<string, string>("displayName", "Admin"),
            new KeyValuePair<string, string>("password", "senha-bem-comprida-123")
        ]));

        if (resposta.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found))
            throw new InvalidOperationException($"Setup falhou: {resposta.StatusCode}");

        var setCookie = resposta.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.FirstOrDefault(v => v.StartsWith("tcmine.auth=", StringComparison.Ordinal))
            : null;

        return setCookie is null
            ? throw new InvalidOperationException("Setup não emitiu cookie de sessão.")
            // Só o par nome=valor interessa ao cabeçalho Cookie; o resto são
            // diretivas de armazenamento que só o browser consome.
            : setCookie.Split(';')[0];
    }

    private static string ExtrairToken(string html)
    {
        var match = Regex.Match(
            html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""");

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("Página de setup não trouxe token de antiforgery.");
    }
}
