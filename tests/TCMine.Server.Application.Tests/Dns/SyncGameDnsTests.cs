using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Dns;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Dns;

/// <summary>
///     A sincronização escreve na zona de DNS do admin. As regras que importam
///     são as de contenção: só toca no que é desta instalação, nunca cria por
///     cima do que já existe, e uma falha num registro não impede os outros.
/// </summary>
public sealed class SyncGameDnsTests
{
    private readonly InstallationSettings _settings = new()
    {
        CloudflareApiTokenEncrypted = "token",
        CloudflareZoneId = new string('a', 32),
        DnsBaseDomain = "exemplo.com"
    };

    private string Prefix => GameDns.CommentPrefix(_settings.Id);

    [Fact]
    public async Task Sem_configuracao_nao_chama_a_cloudflare()
    {
        _settings.CloudflareApiTokenEncrypted = null;
        var dns = new FakeDns();

        var report = await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.False(report.Enabled);
        Assert.Equal(0, dns.Calls);
    }

    [Fact]
    public async Task Cria_o_registro_a_e_um_srv_por_servidor_com_subdominio()
    {
        var dns = new FakeDns();
        var comSub = Servidor("sobrevivencia", 25570);

        var report = await New(dns, "1.2.3.4", comSub, Servidor(null, 25565)).HandleAsync(CancellationToken.None);

        Assert.True(report.Succeeded);
        Assert.Equal(2, report.Created);

        var a = Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.A);
        Assert.Equal("mc.exemplo.com", a.Spec.Name);
        Assert.Equal("1.2.3.4", a.Spec.Target);

        var srv = Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.Srv);
        Assert.Equal("_minecraft._tcp.sobrevivencia.exemplo.com", srv.Spec.Name);
        Assert.Equal("mc.exemplo.com", srv.Spec.Target);
        Assert.Equal(25570, srv.Spec.Port);
        Assert.Equal($"{Prefix}srv:{comSub.Id:N}", srv.Spec.Comment);
    }

    [Fact]
    public async Task Segunda_rodada_igual_nao_escreve_nada()
    {
        var dns = new FakeDns();
        var sync = New(dns, "1.2.3.4", Servidor("a", 25565));
        await sync.HandleAsync(CancellationToken.None);
        var writesAfterFirst = dns.Writes;

        var report = await sync.HandleAsync(CancellationToken.None);

        Assert.True(report.Succeeded);
        Assert.Equal(2, report.Unchanged);
        Assert.Equal(writesAfterFirst, dns.Writes);
    }

    [Fact]
    public async Task Endereco_publico_que_e_nome_vira_o_alvo_e_dispensa_o_registro_a()
    {
        // Quem já tem DDNS não precisa de um segundo nome a apontar para o
        // mesmo IP: os SRV apontam direto para o dele.
        _settings.PublicHost = "casa.ddns.exemplo.net";
        var dns = new FakeDns();

        var report = await New(dns, null, Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.True(report.Succeeded);
        var srv = Assert.Single(dns.Records);
        Assert.Equal("casa.ddns.exemplo.net", srv.Spec.Target);
    }

    [Fact]
    public async Task Endereco_publico_igual_ao_registro_gerenciado_nao_apaga_o_registro()
    {
        // O admin copiou "mc.exemplo.com" para o endereço público. Tratá-lo
        // como nome de fora apagaria o registro A de que ele mesmo depende.
        _settings.PublicHost = "mc.exemplo.com";
        var dns = new FakeDns();

        var report = await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.True(report.Succeeded);
        Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.A);
    }

    [Fact]
    public async Task Ip_nao_detectado_poupa_o_registro_a_e_avisa()
    {
        var dns = new FakeDns();
        var server = Servidor("a", 25565);
        await New(dns, "1.2.3.4", server).HandleAsync(CancellationToken.None);

        var report = await New(dns, null, server).HandleAsync(CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Single(report.Problems);
        Assert.Equal(0, report.Deleted);
        Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.A);
    }

    [Fact]
    public async Task Nao_toca_em_registro_que_nao_tem_a_marca_desta_instalacao()
    {
        var dns = new FakeDns();
        dns.Seed(new DnsRecordSpec("A", "www.exemplo.com", "9.9.9.9", 0, ""));
        dns.Seed(new DnsRecordSpec("SRV", "_minecraft._tcp.x.exemplo.com", "outro.exemplo.com", 25565, "tcmine:outra:srv:1"));

        await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);
        var report = await New(dns, "1.2.3.4").HandleAsync(CancellationToken.None);

        // Sem servidores, o SRV desta instalação sai — e só ele.
        Assert.Equal(1, report.Deleted);
        Assert.Contains(dns.Records, r => r.Spec.Name == "www.exemplo.com");
        Assert.Contains(dns.Records, r => r.Spec.Comment == "tcmine:outra:srv:1");
    }

    [Fact]
    public async Task Nao_cria_por_cima_de_registro_alheio_com_o_mesmo_nome()
    {
        // A Cloudflare aceitaria um segundo A em mc.exemplo.com e passaria a
        // responder ora um IP, ora outro.
        var dns = new FakeDns();
        dns.Seed(new DnsRecordSpec("A", "mc.exemplo.com", "9.9.9.9", 0, "feito à mão"));

        var report = await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Contains(report.Problems, p => p.Contains("mc.exemplo.com", StringComparison.Ordinal));
        Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.A);

        // O SRV, que não conflita com nada, é criado mesmo assim.
        Assert.Equal(1, report.Created);
    }

    [Fact]
    public async Task Falha_ao_listar_nao_toca_em_nada()
    {
        var dns = new FakeDns { FailList = true };

        var report = await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Equal(0, dns.Writes);
    }

    [Fact]
    public async Task Falha_num_registro_nao_impede_os_outros()
    {
        var dns = new FakeDns { FailCreateOf = "_minecraft._tcp.a.exemplo.com" };

        var report = await New(dns, "1.2.3.4", Servidor("a", 25565), Servidor("b", 25566))
            .HandleAsync(CancellationToken.None);

        Assert.Single(report.Problems);
        Assert.Equal(2, report.Created); // o A e o SRV de "b"
    }

    private SyncGameDns New(FakeDns dns, string? detectedIp, params GameServer[] servers) =>
        new(new FakeSettings(_settings), new FakeServers(servers), new FakeDetector(detectedIp), dns);

    private static GameServer Servidor(string? subdomain, int port) => new()
    {
        Name = subdomain ?? "sem-nome",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "",
        GamePort = port,
        Subdomain = subdomain,
        RconSecret = "segredo"
    };

    // ---- Fakes ----

    /// <summary>Uma zona em memória, com os mesmos filtros que a sincronização usa.</summary>
    private sealed class FakeDns : ICloudflareDns
    {
        private int _nextId;

        public List<DnsRecord> Records { get; } = [];
        public bool FailList { get; init; }
        public string? FailCreateOf { get; init; }
        public int Calls { get; private set; }
        public int Writes { get; private set; }

        public void Seed(DnsRecordSpec spec) => Records.Add(new DnsRecord($"seed{_nextId++}", spec));

        public Task<IReadOnlyList<DnsRecord>> ListByCommentPrefixAsync(
            DnsZone zone, string commentPrefix, CancellationToken ct)
        {
            Calls++;

            if (FailList)
                throw new DnsProviderException("token recusado");

            return Task.FromResult<IReadOnlyList<DnsRecord>>(
                [.. Records.Where(r => r.Spec.Comment.StartsWith(commentPrefix, StringComparison.Ordinal))]);
        }

        public Task<IReadOnlyList<DnsRecord>> ListByNameAsync(DnsZone zone, string name, CancellationToken ct)
        {
            Calls++;

            return Task.FromResult<IReadOnlyList<DnsRecord>>(
                [.. Records.Where(r => string.Equals(r.Spec.Name, name, StringComparison.OrdinalIgnoreCase))]);
        }

        public Task CreateAsync(DnsZone zone, DnsRecordSpec record, CancellationToken ct)
        {
            Calls++;

            if (record.Name == FailCreateOf)
                throw new DnsProviderException("recusado");

            Writes++;
            Records.Add(new DnsRecord($"id{_nextId++}", record));
            return Task.CompletedTask;
        }

        public Task ReplaceAsync(DnsZone zone, string recordId, DnsRecordSpec record, CancellationToken ct)
        {
            Calls++;
            Writes++;
            Records[Records.FindIndex(r => r.Id == recordId)] = new DnsRecord(recordId, record);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DnsZone zone, string recordId, CancellationToken ct)
        {
            Calls++;
            Writes++;
            Records.RemoveAll(r => r.Id == recordId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeServers(GameServer[] servers) : FakeServerRepositoryBase
    {
        public override Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>(servers);
    }

    private sealed class FakeDetector(string? address) : IPublicAddressProvider
    {
        public Task<string?> GetAsync(CancellationToken ct) => Task.FromResult(address);
    }

    private sealed class FakeSettings(InstallationSettings settings) : ISettingsRepository
    {
        public Task<InstallationSettings> GetAsync(CancellationToken ct) => Task.FromResult(settings);

        public Task SaveAsync(InstallationSettings s, CancellationToken ct) => Task.CompletedTask;

        public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
