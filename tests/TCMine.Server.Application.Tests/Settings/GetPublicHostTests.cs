using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Settings;

/// <summary>
///     A precedência do host público: o que o admin gravou vence o que foi
///     detectado. E a detecção sai para a rede, então só acontece quando alguém
///     precisa dela.
/// </summary>
public sealed class GetPublicHostTests
{
    [Fact]
    public async Task Sem_host_gravado_vale_o_ip_detectado()
    {
        var detector = new FakeDetector("1.2.3.4");

        var host = await New(null, detector).HandleAsync(false, CancellationToken.None);

        Assert.Null(host.Configured);
        Assert.Equal("1.2.3.4", host.Effective);
    }

    [Fact]
    public async Task Host_gravado_vence_e_dispensa_a_deteccao()
    {
        // É o caminho da lista de servidores do launcher: com o host gravado,
        // nenhuma chamada sai para a rede.
        var detector = new FakeDetector("1.2.3.4");

        var host = await New("jogar.exemplo.com", detector).HandleAsync(false, CancellationToken.None);

        Assert.Equal("jogar.exemplo.com", host.Effective);
        Assert.Equal(0, detector.Calls);
    }

    [Fact]
    public async Task A_tela_de_configuracoes_ve_os_dois()
    {
        var host = await New("jogar.exemplo.com", new FakeDetector("1.2.3.4"))
            .HandleAsync(true, CancellationToken.None);

        Assert.Equal("jogar.exemplo.com", host.Configured);
        Assert.Equal("1.2.3.4", host.Detected);
        Assert.Equal("jogar.exemplo.com", host.Effective);
    }

    [Fact]
    public async Task Sem_host_e_sem_deteccao_nao_ha_host()
    {
        var host = await New("   ", new FakeDetector(null)).HandleAsync(false, CancellationToken.None);

        Assert.Null(host.Effective);
    }

    private static GetPublicHost New(string? stored, FakeDetector detector) =>
        new(new FakeSettings(new InstallationSettings { PublicHost = stored }), detector);

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
