using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Settings;

/// <summary>
///     O teste de envio existe para o admin não descobrir que o SMTP está
///     errado no dia em que alguém esquece a senha. Por isso a garantia que mais
///     importa aqui é a primeira: sem SMTP configurado, ele precisa FALHAR — o
///     envio cairia no log e a tela diria "enviado" sem ninguém ter recebido
///     nada.
/// </summary>
public sealed class SendTestEmailTests
{
    [Fact]
    public async Task Sem_smtp_configurado_recusa_em_vez_de_fingir_sucesso()
    {
        var email = new FakeEmail();

        var result = await new SendTestEmail(email, new FakeSettings(new InstallationSettings()))
            .HandleAsync("ana@teste.com", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        email.Enviados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Com_smtp_configurado_envia_para_o_endereco_informado()
    {
        var email = new FakeEmail();

        var result = await new SendTestEmail(email, new FakeSettings(Configurado()))
            .HandleAsync("  ana@teste.com  ", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        email.Enviados.ShouldHaveSingleItem().To.ShouldBe("ana@teste.com");
    }

    [Fact]
    public async Task Endereco_vazio_e_recusado()
    {
        var email = new FakeEmail();

        var result = await new SendTestEmail(email, new FakeSettings(Configurado()))
            .HandleAsync("   ", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        email.Enviados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Falha_do_servidor_volta_com_a_causa_e_nao_como_excecao()
    {
        // Autenticação recusada, host errado, TLS incompatível: a mensagem do
        // SMTP é justamente o que o admin precisa ler para consertar.
        var email = new FakeEmail { Erro = "535 authentication failed" };

        var result = await new SendTestEmail(email, new FakeSettings(Configurado()))
            .HandleAsync("ana@teste.com", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("535 authentication failed");
    }

    private static InstallationSettings Configurado() => new()
    {
        SmtpHost = "smtp.teste.com",
        SmtpFrom = "TCMine <nao-responda@teste.com>"
    };

    private sealed class FakeSettings(InstallationSettings settings) : ISettingsRepository
    {
        public Task<InstallationSettings> GetAsync(CancellationToken ct) => Task.FromResult(settings);
        public Task SaveAsync(InstallationSettings s, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> GetSmtpPasswordAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed class FakeEmail : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Enviados { get; } = [];
        public string? Erro { get; init; }

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            if (Erro is not null)
                throw new InvalidOperationException(Erro);

            Enviados.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
