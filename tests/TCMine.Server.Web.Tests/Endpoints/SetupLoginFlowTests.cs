using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     A volta entre /admin/setup e /admin/login.
///     Salvar o Client ID no setup NÃO cria usuário nenhum — só o primeiro
///     login de verdade cria. Um guard em /admin/login que mandasse de volta
///     ao setup por "não existe usuário ainda" prendia o admin num loop: ele
///     nunca chegava a ver o botão "Entrar com a Microsoft" para criar o
///     primeiro usuário. O guard certo é sobre o Client ID estar configurado,
///     não sobre existir usuário.
/// </summary>
public sealed class SetupLoginFlowTests
{
    [Fact]
    public async Task Sem_client_id_configurado_login_manda_para_o_setup()
    {
        await using var factory = new TcMineAppFactory("Development", ("Server:AzureClientId", ""));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/login", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        // O redirect de uma página SSR estática (NavigationManager.NavigateTo
        // durante OnInitializedAsync, convertido em 302 pelo framework) sai
        // como URI ABSOLUTA ("http://localhost/admin/setup"), não relativa —
        // StartsWith("/admin/setup") contra isso falha sempre, mesmo quando o
        // destino está certo. AbsolutePath normaliza os dois formatos.
        var location = response.Headers.Location!;
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.ToString();
        path.ShouldStartWith("/admin/setup");
    }

    [Fact]
    public async Task Client_id_configurado_mas_sem_usuario_ainda_login_nao_volta_para_o_setup()
    {
        // O caso que quebrava: Client ID já salvo (via /auth/setup, de
        // verdade), ninguém logou ainda. /admin/login tem de mostrar o botão,
        // não devolver ao /setup.
        await using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await SalvarClientIdAsync(client);

        var response = await client.GetAsync("/admin/login", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task SalvarClientIdAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/admin/setup", TestContext.Current.CancellationToken);
        var token = Regex.Match(
            html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var response = await client.PostAsync(
            "/auth/setup",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("azureClientId", "22222222-2222-2222-2222-222222222222")
            ]),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }
}
