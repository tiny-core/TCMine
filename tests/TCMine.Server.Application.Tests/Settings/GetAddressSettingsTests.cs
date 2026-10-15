using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Settings;

/// <summary>
///     As precedências do endereço: o host que o admin gravou vence o detectado,
///     a detecção só sai para a rede quando alguém precisa dela, e o subdomínio
///     só vale com o DNS configurado por inteiro.
/// </summary>
public sealed class GetAddressSettingsTests
{
    [Fact]
    public async Task Sem_host_gravado_vale_o_ip_detectado()
    {
        var detector = new FakeDetector("1.2.3.4");

        var address = await New(new InstallationSettings(), detector).HandleAsync(false, CancellationToken.None);

        Assert.Null(address.ConfiguredHost);
        Assert.Equal("1.2.3.4", address.PublicHost);
    }

    [Fact]
    public async Task Host_gravado_vence_e_dispensa_a_deteccao()
    {
        // É o caminho da lista de servidores do launcher: com o host gravado,
        // nenhuma chamada sai para a rede.
        var detector = new FakeDetector("1.2.3.4");

        var address = await New(new InstallationSettings { PublicHost = "jogar.exemplo.com" }, detector)
            .HandleAsync(false, CancellationToken.None);

        Assert.Equal("jogar.exemplo.com", address.PublicHost);
        Assert.Equal(0, detector.Calls);
    }

    [Fact]
    public async Task A_tela_de_configuracoes_ve_os_dois()
    {
        var address = await New(new InstallationSettings { PublicHost = "jogar.exemplo.com" }, new FakeDetector("1.2.3.4"))
            .HandleAsync(true, CancellationToken.None);

        Assert.Equal("jogar.exemplo.com", address.ConfiguredHost);
        Assert.Equal("1.2.3.4", address.DetectedHost);
        Assert.Equal("jogar.exemplo.com", address.PublicHost);
    }

    [Fact]
    public async Task Sem_host_e_sem_deteccao_nao_ha_host()
    {
        var address = await New(new InstallationSettings { PublicHost = "   " }, new FakeDetector(null))
            .HandleAsync(false, CancellationToken.None);

        Assert.Null(address.PublicHost);
    }

    [Fact]
    public async Task Com_o_dns_configurado_o_subdominio_vira_o_endereco()
    {
        var address = await New(DnsConfigured(), new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Equal("sobrevivencia.exemplo.com", address.For(Servidor("", 25570, "sobrevivencia")));

        // Sem subdomínio sai pelo registro A que o TCMine mantém, e não pelo
        // IP: o nome continua certo quando o IP de casa muda.
        Assert.Equal("mc.exemplo.com:25570", address.For(Servidor("", 25570, null)));
    }

    [Fact]
    public async Task No_modo_automatico_o_dominio_guardado_nao_vale()
    {
        // O admin tinha um domínio e voltou para o automático: o domínio fica
        // guardado (voltar não pede para redigitar) e os jogadores recebem o IP.
        var settings = new InstallationSettings { NetworkMode = NetworkMode.Automatic, PublicHost = "jogar.exemplo.com" };

        var address = await New(settings, new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Equal("1.2.3.4", address.PublicHost);
    }

    [Fact]
    public async Task No_modo_meu_dominio_a_cloudflare_guardada_nao_vale()
    {
        var settings = DnsConfigured();
        settings.NetworkMode = NetworkMode.OwnHost;
        settings.PublicHost = "jogar.exemplo.com";

        var address = await New(settings, new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Null(address.DnsBaseDomain);
        Assert.Equal("jogar.exemplo.com:25570", address.For(Servidor("", 25570, "sobrevivencia")));
    }

    [Fact]
    public async Task Cloudflare_com_a_opcao_avancada_publica_o_ddns()
    {
        var settings = DnsConfigured();
        settings.NetworkMode = NetworkMode.Cloudflare;
        settings.CloudflareUsesPublicHost = true;
        settings.PublicHost = "casa.ddns.exemplo.net";

        var address = await New(settings, new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Equal("casa.ddns.exemplo.net:25570", address.For(Servidor("", 25570, null)));
        Assert.Equal("sobrevivencia.exemplo.com", address.For(Servidor("", 25570, "sobrevivencia")));
    }

    [Fact]
    public async Task Deteccao_desligada_no_modo_meu_dominio_nao_sai_para_a_rede()
    {
        // Nem a tela de configurações, que pede os dois lado a lado.
        var detector = new FakeDetector("1.2.3.4");
        var settings = new InstallationSettings
        {
            NetworkMode = NetworkMode.OwnHost,
            PublicHost = "jogar.exemplo.com",
            PublicIpDetectionDisabled = true
        };

        var address = await New(settings, detector).HandleAsync(true, CancellationToken.None);

        Assert.Null(address.DetectedHost);
        Assert.Equal("jogar.exemplo.com", address.PublicHost);
        Assert.Equal(0, detector.Calls);
    }

    [Theory]
    [InlineData(NetworkMode.Automatic, false, true)]
    [InlineData(NetworkMode.Cloudflare, false, true)] // o registro A é escrito com o IP detectado
    [InlineData(NetworkMode.Cloudflare, true, false)] // apontando para o DDNS do admin
    [InlineData(NetworkMode.OwnHost, false, false)]
    public void Desligar_a_deteccao_so_vale_onde_o_modo_nao_depende_dela(
        NetworkMode mode, bool usesPublicHost, bool stillDetects)
    {
        // Guardado de quando o modo era outro, o "desligado" não pode deixar o
        // automático sem endereço.
        var settings = new InstallationSettings
        {
            NetworkMode = mode,
            CloudflareUsesPublicHost = usesPublicHost,
            PublicIpDetectionDisabled = true
        };

        settings.PublicIpDetectionEnabled.ShouldBe(stillDetects);
    }

    [Fact]
    public async Task Sonda_do_caminho_direto_tem_sempre_host_e_porta()
    {
        // O subdomínio é um SRV, que a sonda TCP do launcher não lê: ela vai ao
        // registro A com a porta do servidor.
        var address = await New(DnsConfigured(), new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Equal("mc.exemplo.com:25570", address.ProbeFor(Servidor("", 25570, "sobrevivencia")));
        Assert.Equal("jogo.outro.com:30000", address.ProbeFor(Servidor("jogo.outro.com:30000", 25570, null)));
        Assert.Equal("jogo.outro.com:25570", address.ProbeFor(Servidor("jogo.outro.com", 25570, null)));
    }

    [Fact]
    public async Task Nome_do_tunel_so_sai_com_o_tunel_configurado()
    {
        var server = Servidor("", 25570, null);
        server.TunnelLabel = "atm10-tunel";

        var semTunel = await New(DnsConfigured(), new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);
        Assert.Null(semTunel.TunnelFor(server));

        var settings = DnsConfigured();
        settings.CloudflareAccountId = new string('b', 32);
        settings.CloudflareTunnelId = "11111111-2222-3333-4444-555555555555";
        var comTunel = await New(settings, new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Equal("atm10-tunel.exemplo.com", comTunel.TunnelFor(server));
    }

    [Fact]
    public void Sem_escolha_gravada_o_modo_sai_do_que_esta_preenchido()
    {
        // Instalações de antes da escolha: o resultado tem de ser o mesmo que
        // elas já tinham, sem migration reescrever nada.
        new InstallationSettings().EffectiveNetworkMode.ShouldBe(NetworkMode.Automatic);
        new InstallationSettings { PublicHost = "x.exemplo.com" }.EffectiveNetworkMode.ShouldBe(NetworkMode.OwnHost);
        DnsConfigured().EffectiveNetworkMode.ShouldBe(NetworkMode.Cloudflare);
    }

    [Fact]
    public async Task Sem_token_o_subdominio_nao_entra_no_endereco()
    {
        // O domínio sozinho não basta: sem token ninguém mantém o registro, e
        // publicar o nome seria mandar os jogadores a um endereço que não existe.
        var settings = DnsConfigured();
        settings.CloudflareApiTokenEncrypted = null;

        var address = await New(settings, new FakeDetector("1.2.3.4")).HandleAsync(false, CancellationToken.None);

        Assert.Null(address.DnsBaseDomain);
        Assert.Equal("1.2.3.4:25570", address.For(Servidor("", 25570, "sobrevivencia")));
    }

    private static InstallationSettings DnsConfigured() => new()
    {
        CloudflareApiTokenEncrypted = "token",
        CloudflareZoneId = new string('a', 32),
        DnsBaseDomain = "exemplo.com"
    };

    private static GameServer Servidor(string address, int port, string? subdomain) => new()
    {
        Name = "Servidor",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = address,
        GamePort = port,
        Subdomain = subdomain,
        RconSecret = "segredo"
    };

    private static GetAddressSettings New(InstallationSettings settings, FakeDetector detector) =>
        new(new FakeSettings(settings), detector);

    private sealed class FakeDetector(string? address) : IPublicAddressProvider
    {
        public int Calls { get; private set; }

        public Task<string?> GetAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(address);
        }
    }

    private sealed class FakeSettings(InstallationSettings settings) : ISettingsRepository
    {
        public Task<InstallationSettings> GetAsync(CancellationToken ct) => Task.FromResult(settings);

        public Task SaveAsync(InstallationSettings s, CancellationToken ct) => Task.CompletedTask;

        public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
