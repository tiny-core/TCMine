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

        // Servidor sem subdomínio continua no automático.
        Assert.Equal("1.2.3.4:25570", address.For(Servidor("", 25570, null)));
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
