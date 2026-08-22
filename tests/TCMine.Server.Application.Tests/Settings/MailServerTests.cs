using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Settings;

/// <summary>
///     O servidor de e-mail gerenciado pelo painel.
///     Subir o container é a parte fácil; o que decide se a mensagem chega é o
///     DNS, que fica fora do alcance do TCMine. Por isso os testes se
///     concentram em duas coisas: que o SMTP fica apontado para o servidor sem o
///     admin digitar nada, e que os registros entregues para colar no DNS estão
///     corretos.
/// </summary>
public sealed class MailServerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Subir_deixa_o_smtp_apontado_sem_o_admin_digitar_nada()
    {
        // A senha é gerada aqui e nunca exibida: se o admin tivesse de
        // digitá-la, erraria — e o erro apareceria só na primeira recuperação
        // de senha de alguém.
        var settings = new FakeSettings();
        var orchestrator = new FakeOrchestrator();

        var result = await new StartMailServer(orchestrator, settings, Admin())
            .HandleAsync("Exemplo.COM.", Ct);

        result.Succeeded.ShouldBeTrue();

        settings.Atual.MailServerDomain.ShouldBe("exemplo.com");
        settings.Atual.SmtpHost.ShouldBe("127.0.0.1");
        settings.Atual.SmtpPort.ShouldBe(587);
        settings.Atual.SmtpUser.ShouldBe("nao-responda@exemplo.com");
        settings.Atual.SmtpFrom!.ShouldContain("nao-responda@exemplo.com");
        settings.Atual.HasSmtp.ShouldBeTrue();

        // A conta precisa existir do lado do servidor, com a mesma senha.
        orchestrator.ContaCriada.ShouldBe("nao-responda@exemplo.com");
        orchestrator.SenhaDaConta.ShouldBe(settings.Atual.SmtpPasswordEncrypted);
    }

    [Theory]
    [InlineData("nao-e-dominio")]
    [InlineData("espaço.com")]
    [InlineData("a.b")]
    [InlineData("")]
    public async Task Dominio_invalido_nao_sobe_nada(string dominio)
    {
        var orchestrator = new FakeOrchestrator();

        var result = await new StartMailServer(orchestrator, new FakeSettings(), Admin())
            .HandleAsync(dominio, Ct);

        result.Succeeded.ShouldBeFalse();
        orchestrator.Subiu.ShouldBeFalse();
    }

    [Fact]
    public async Task So_o_admin_da_instalacao_sobe_o_servidor_de_email()
    {
        // Não é recurso de um servidor de jogo: é da instalação. Owner de um
        // servidor não manda no e-mail de todos os outros.
        var orchestrator = new FakeOrchestrator();

        var result = await new StartMailServer(orchestrator, new FakeSettings(), NaoAdmin())
            .HandleAsync("exemplo.com", Ct);

        result.Succeeded.ShouldBeFalse();
        orchestrator.Subiu.ShouldBeFalse();
    }

    [Fact]
    public async Task Falha_ao_subir_devolve_a_causa_e_nao_mexe_na_configuracao()
    {
        // Docker fora do ar, imagem que não baixa, porta 587 ocupada. Gravar o
        // SMTP apontando para um servidor que não subiu deixaria a instalação
        // pior que antes: com envio configurado e quebrado.
        var settings = new FakeSettings();
        var orchestrator = new FakeOrchestrator { Erro = "porta 587 em uso" };

        var result = await new StartMailServer(orchestrator, settings, Admin())
            .HandleAsync("exemplo.com", Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("porta 587 em uso");
        settings.Gravou.ShouldBeFalse();
    }

    [Fact]
    public async Task So_o_admin_da_instalacao_para_o_servidor()
    {
        var orchestrator = new FakeOrchestrator();

        var result = await new StopMailServer(orchestrator, NaoAdmin()).HandleAsync(Ct);

        result.Succeeded.ShouldBeFalse();
        orchestrator.Parou.ShouldBeFalse();
    }

    [Fact]
    public void Registros_de_dns_cobrem_spf_dmarc_e_o_nome_do_servidor()
    {
        var registros = MailDnsRecords.For("exemplo.com", null, "203.0.113.10");

        registros.ShouldContain(r => r.Type == "TXT" && r.Value.StartsWith("v=spf1"));
        registros.ShouldContain(r => r.Name == "_dmarc.exemplo.com");
        registros.ShouldContain(r => r.Type == "MX");
        registros.ShouldContain(r => r.Name == "mail.exemplo.com" && r.Value == "203.0.113.10");
    }

    [Fact]
    public void Dkim_so_aparece_depois_de_a_chave_existir()
    {
        // Um registro DKIM com valor inventado é pior que registro nenhum: o
        // destinatário passa a exigir assinatura e nenhuma bate.
        MailDnsRecords.For("exemplo.com", null, null)
            .ShouldNotContain(r => r.Name.Contains("_domainkey"));

        MailDnsRecords.For("exemplo.com", "v=DKIM1; k=rsa; p=ABC", null)
            .ShouldContain(r => r.Name == "mail._domainkey.exemplo.com" && r.Value.Contains("p=ABC"));
    }

    [Fact]
    public void Dominio_com_ponto_final_e_maiuscula_normaliza()
    {
        // O admin cola o domínio como o painel de DNS mostra, às vezes com o
        // ponto final da notação de zona.
        MailDnsRecords.For("Exemplo.COM.", null, null)
            .ShouldContain(r => r.Name == "_dmarc.exemplo.com");
    }

    [Fact]
    public async Task Sem_dominio_configurado_a_tela_nao_promete_registros()
    {
        var view = await new GetMailServerView(new FakeOrchestrator(), new FakeSettings())
            .HandleAsync(Ct);

        view.Domain.ShouldBeNull();
        view.DnsRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task Docker_inacessivel_nao_derruba_a_tela_de_configuracao()
    {
        // A tela de Configurações precisa abrir mesmo sem Docker: é onde o admin
        // vai justamente para configurar SMTP de terceiro e não usar container.
        var view = await new GetMailServerView(
                new FakeOrchestrator { Erro = "docker fora do ar" }, new FakeSettings())
            .HandleAsync(Ct);

        view.State.ShouldBe(MailServerState.NotCreated);
    }

    private static FakeUserScope Admin() => new() { IsInstanceAdmin = true };

    private static FakeUserScope NaoAdmin() => new(ServerRoleDto.Owner) { IsInstanceAdmin = false };

    private sealed class FakeOrchestrator : IMailServerOrchestrator
    {
        public string? Erro { get; init; }
        public bool Subiu { get; private set; }
        public bool Parou { get; private set; }
        public string? ContaCriada { get; private set; }
        public string? SenhaDaConta { get; private set; }

        public Task<MailServerState> GetStateAsync(CancellationToken ct) =>
            Erro is not null
                ? throw new InvalidOperationException(Erro)
                : Task.FromResult(Subiu ? MailServerState.Running : MailServerState.NotCreated);

        public Task StartAsync(string domain, CancellationToken ct)
        {
            if (Erro is not null)
                throw new InvalidOperationException(Erro);

            Subiu = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            Parou = true;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<string?> GetDkimRecordAsync(string domain, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task EnsureSenderAccountAsync(string address, string password, CancellationToken ct)
        {
            ContaCriada = address;
            SenhaDaConta = password;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettings : ISettingsRepository
    {
        public InstallationSettings Atual { get; } = new();
        public bool Gravou { get; private set; }

        public Task<InstallationSettings> GetAsync(CancellationToken ct) => Task.FromResult(Atual);

        public Task SaveAsync(InstallationSettings settings, CancellationToken ct)
        {
            Gravou = true;
            return Task.CompletedTask;
        }

        public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> GetSmtpPasswordAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
