using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Um POST de formulário para /auth/* sem o token de antiforgery tem de ser
///     recusado ANTES de chegar ao caso de uso — senão um site de terceiros
///     poderia auto-submeter um formulário de login com as credenciais do
///     atacante no navegador da vítima (login CSRF).
///     A proteção aqui não é um filtro escrito à mão: desde o .NET 8, um
///     parâmetro <c>[FromForm]</c> em minimal API já EXIGE antiforgery por
///     padrão (silenciosamente, via metadata que o binder de formulário anexa
///     ao endpoint), então não há "RequireAntiforgeryToken()" para chamar — só
///     é preciso não desligar com DisableAntiforgery(). Este teste existe para
///     travar isso: se algum dia alguém adicionar DisableAntiforgery() aqui
///     "para simplificar um teste que falha", ele quebra.
/// </summary>
public class CsrfProtectionTests
{
    [Fact]
    public async Task Login_sem_token_antiforgery_e_recusado()
    {
        using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent([
                new KeyValuePair<string, string>("email", "ninguem@teste.local"),
                new KeyValuePair<string, string>("password", "qualquer")
            ]),
            TestContext.Current.CancellationToken);

        // 400 antes de qualquer credencial ser avaliada: um 302 para /login
        // com ?error= significaria que o caso de uso RODOU sem token nenhum.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Setup_sem_token_antiforgery_e_recusado()
    {
        using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            "/auth/setup",
            new FormUrlEncodedContent([
                new KeyValuePair<string, string>("email", "admin@teste.local"),
                new KeyValuePair<string, string>("displayName", "Admin"),
                new KeyValuePair<string, string>("password", "SenhaForte123!")
            ]),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
