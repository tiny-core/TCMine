using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     A tela de login, renderizada de verdade.
///     Vale o custo de subir a aplicação porque a decisão que se testa aqui é
///     visual e condicional: oferecer recuperação de senha sem ter como enviar
///     e-mail manda a pessoa esperar por uma mensagem que nunca chega — e a
///     resposta do formulário é a mesma para e-mail existente e inexistente, de
///     propósito, então nem o silêncio a alertaria.
/// </summary>
public sealed class LoginPageTests
{
    [Fact]
    public async Task Sem_smtp_a_tela_nao_oferece_recuperacao()
    {
        await using var factory = new TcMineAppFactory();
        await factory.EntrarComoAdminAsync();

        var html = await factory.CreateClient().GetStringAsync(
            "/login", TestContext.Current.CancellationToken);

        html.ShouldNotContain("/forgot-password");
        html.ShouldContain("não tem envio de e-mail configurado");
    }

    [Fact]
    public async Task Com_smtp_a_tela_oferece_recuperacao()
    {
        await using var factory = new TcMineAppFactory();
        await factory.EntrarComoAdminAsync();
        await ConfigurarSmtpAsync(factory);

        var html = await factory.CreateClient().GetStringAsync(
            "/login", TestContext.Current.CancellationToken);

        html.ShouldContain("/forgot-password");
    }

    private static async Task ConfigurarSmtpAsync(TcMineAppFactory factory)
    {
        using var escopo = factory.Services.CreateScope();
        var db = await escopo.ServiceProvider
            .GetRequiredService<IDbContextFactory<TcMineDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        // A linha de configuração nasce na primeira leitura; garante que existe
        // antes de mexer nela.
        var settings = await db.InstallationSettings.FirstOrDefaultAsync(
            TestContext.Current.CancellationToken);

        if (settings is null)
        {
            settings = new Server.Domain.Settings.InstallationSettings();
            db.InstallationSettings.Add(settings);
        }

        settings.SmtpHost = "smtp.teste.com";
        settings.SmtpFrom = "TCMine <nao-responda@teste.com>";

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
