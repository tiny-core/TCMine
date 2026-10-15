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
    public async Task Dominio_guardado_de_outro_modo_nao_vira_o_alvo()
    {
        // O admin usava "meu domínio" e passou para a Cloudflare sem a opção
        // avançada: o domínio antigo fica guardado e não pode desviar os SRV
        // para um nome que ele talvez nem mantenha mais.
        _settings.NetworkMode = NetworkMode.Cloudflare;
        _settings.PublicHost = "casa.ddns.exemplo.net";
        var dns = new FakeDns();

        await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.Equal("mc.exemplo.com", Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.Srv).Spec.Target);
    }

    [Fact]
    public async Task Opcao_avancada_aponta_os_registros_para_o_ddns()
    {
        _settings.NetworkMode = NetworkMode.Cloudflare;
        _settings.CloudflareUsesPublicHost = true;
        _settings.PublicHost = "casa.ddns.exemplo.net";
        var dns = new FakeDns();

        await New(dns, null, Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.Equal("casa.ddns.exemplo.net", Assert.Single(dns.Records).Spec.Target);
    }

    [Fact]
    public async Task Outro_modo_escolhido_nao_mexe_na_zona()
    {
        // Credenciais guardadas não bastam: com outro modo escolhido, a
        // Cloudflare está desligada por decisão do admin.
        _settings.NetworkMode = NetworkMode.Automatic;
        var dns = new FakeDns();

        var report = await New(dns, "1.2.3.4", Servidor("a", 25565)).HandleAsync(CancellationToken.None);

        Assert.False(report.Enabled);
        Assert.Equal(0, dns.Calls);
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

    // ---- Túnel ----

    [Fact]
    public async Task Servidor_com_tunel_ganha_cname_pela_nuvem_e_entrada_tcp()
    {
        LigarTunel();
        var dns = new FakeDns();
        var server = Servidor(null, 25570);
        server.TunnelLabel = "atm10-tunel";

        var report = await New(dns, "1.2.3.4", server).HandleAsync(CancellationToken.None);

        Assert.True(report.Succeeded);
        var cname = Assert.Single(dns.Records, r => r.Spec.Type == DnsRecordTypes.Cname);
        Assert.Equal("atm10-tunel.exemplo.com", cname.Spec.Name);
        Assert.Equal("11111111-2222-3333-4444-555555555555.cfargotunnel.com", cname.Spec.Target);

        var entrada = Assert.Single(_tunnel.Rules, r => r.Hostname is not null);
        Assert.Equal("atm10-tunel.exemplo.com", entrada.Hostname);
        Assert.Equal("tcp://localhost:25570", entrada.Service);
        Assert.Equal(TunnelIngressPlan.CatchAllService, _tunnel.Rules[^1].Service);
    }

    [Fact]
    public async Task Entrada_do_admin_no_tunel_sobrevive()
    {
        // O mesmo túnel serve o próprio painel: a API troca a lista inteira, e
        // perder a entrada do painel derrubaria o TCMine.
        LigarTunel();
        _tunnel.Rules =
        [
            new TunnelIngressRule("tcmine.exemplo.com", "http://localhost:8080", null, "{}"),
            new TunnelIngressRule(null, TunnelIngressPlan.CatchAllService, null, "{}")
        ];
        var server = Servidor(null, 25570);
        server.TunnelLabel = "atm10-tunel";

        await New(new FakeDns(), "1.2.3.4", server).HandleAsync(CancellationToken.None);

        Assert.Contains(_tunnel.Rules, r => r.Hostname == "tcmine.exemplo.com");
        Assert.Contains(_tunnel.Rules, r => r.Hostname == "atm10-tunel.exemplo.com");
    }

    [Fact]
    public async Task Tunel_por_arquivo_vira_aviso_e_nao_e_gravado()
    {
        LigarTunel();
        _tunnel.RemotelyManaged = false;
        var server = Servidor(null, 25570);
        server.TunnelLabel = "atm10-tunel";

        var report = await New(new FakeDns(), "1.2.3.4", server).HandleAsync(CancellationToken.None);

        Assert.Contains(report.Problems, p => p.Contains("config.yml", StringComparison.Ordinal));
        Assert.Equal(0, _tunnel.Puts);
    }

    [Fact]
    public async Task Sem_os_ids_do_tunel_nada_vai_para_ele()
    {
        var server = Servidor(null, 25570);
        server.TunnelLabel = "atm10-tunel";
        var dns = new FakeDns();

        await New(dns, "1.2.3.4", server).HandleAsync(CancellationToken.None);

        Assert.Equal(0, _tunnel.Gets);
        Assert.DoesNotContain(dns.Records, r => r.Spec.Type == DnsRecordTypes.Cname);
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

    private readonly FakeTunnel _tunnel = new();

    private SyncGameDns New(FakeDns dns, string? detectedIp, params GameServer[] servers) =>
        new(new FakeSettings(_settings), new FakeServers(servers), new FakeDetector(detectedIp), dns, _tunnel);

    private void LigarTunel()
    {
        _settings.CloudflareAccountId = new string('b', 32);
        _settings.CloudflareTunnelId = "11111111-2222-3333-4444-555555555555";
    }

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

    /// <summary>Um túnel em memória: lê e grava a lista inteira, como a API.</summary>
    private sealed class FakeTunnel : ICloudflareTunnel
    {
        public IReadOnlyList<TunnelIngressRule> Rules { get; set; } = [];
        public bool RemotelyManaged { get; set; } = true;
        public int Gets { get; private set; }
        public int Puts { get; private set; }

        public Task<TunnelIngress> GetIngressAsync(TunnelAccount account, CancellationToken ct)
        {
            Gets++;
            return Task.FromResult(new TunnelIngress(Rules, RemotelyManaged, "{}"));
        }

        public Task PutIngressAsync(TunnelAccount account, TunnelIngress ingress, CancellationToken ct)
        {
            Puts++;
            Rules = ingress.Rules;
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
