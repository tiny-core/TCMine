using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     A API da nuvem com o pipeline real (filtro da chave, limite de taxa,
///     SQLite com as migrations), fazendo o que o mod tccloud faz: hello,
///     acquire, lotes, release. Cada teste é um cenário do plano
///     (docs/CLOUD-STORAGE.md) — principalmente os que não podem duplicar item.
/// </summary>
public sealed class CloudApiContractTests
{
    private const string Jogador = "069a79f444e94726a5befca90e38aaf5";
    private static readonly string Diamante = new('d', 64);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- autenticação

    [Fact]
    public async Task Sem_chave_ou_com_chave_errada_e_401()
    {
        await using var env = await Ambiente.CriarAsync();

        var semChave = env.Factory.CreateClient();
        (await semChave.PostAsJsonAsync("/api/cloud/v1/hello", Hello(), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

        var errada = env.Cliente(env.ChaveA[..^2] + "xx");
        (await errada.PostAsJsonAsync("/api/cloud/v1/hello", Hello(), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Servidor_desligado_da_nuvem_perde_o_acesso_na_hora()
    {
        await using var env = await Ambiente.CriarAsync();
        await env.AlterarServidorAsync(env.ServidorA, s => s.DetachFromCloudVault());

        var resposta = await env.Cliente(env.ChaveA).PostAsJsonAsync("/api/cloud/v1/hello", Hello(), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protocolo_diferente_e_426()
    {
        await using var env = await Ambiente.CriarAsync();

        var resposta = await env.Cliente(env.ChaveA)
            .PostAsJsonAsync("/api/cloud/v1/hello", Hello() with { Protocol = 99 }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.UpgradeRequired);
    }

    [Fact]
    public async Task Hello_devolve_a_configuracao_da_nuvem()
    {
        await using var env = await Ambiente.CriarAsync();

        var reply = await PostAsync<CloudHelloReply>(env.Cliente(env.ChaveA), "/api/cloud/v1/hello", Hello());

        reply.Protocol.ShouldBe(CloudProtocol.Current);
        reply.MaxItemBytes.ShouldBe(8192);
        reply.ReadOnly.ShouldBeFalse();
    }

    // ---------------------------------------------------------------- lease

    [Fact]
    public async Task Primeiro_acquire_cria_o_canal_principal_e_outro_servidor_fica_esperando()
    {
        await using var env = await Ambiente.CriarAsync();

        var a = await Acquire(env.Cliente(env.ChaveA));
        a.Status.ShouldBe("granted");
        a.Epoch.ShouldBe(1);
        a.Channels.Single().Name.ShouldBe(CloudChannel.DefaultName);

        var b = await Acquire(env.Cliente(env.ChaveB));
        b.Status.ShouldBe("busy");
        b.Holder.ShouldBe("Servidor A");
    }

    [Fact]
    public async Task Lease_vencido_passa_para_outro_servidor_e_o_heartbeat_do_antigo_avisa()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        await Acquire(a);

        env.Relogio.Avancar(TimeSpan.FromMinutes(31));
        (await Acquire(env.Cliente(env.ChaveB))).Epoch.ShouldBe(2);

        var hb = await PostAsync<CloudHeartbeatReply>(a, "/api/cloud/v1/leases/heartbeat",
            new CloudHeartbeatRequest([new CloudHeldLeaseDto(Jogador, 1)]));
        hb.Lost.ShouldBe([Jogador]);
    }

    // ---------------------------------------------------------------- lotes

    [Fact]
    public async Task Itens_guardados_num_servidor_aparecem_no_outro_depois_do_release()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;

        (await Lote(a, Credito(canal, seq: 1, delta: 64, depois: 64))).Result.ShouldBe("applied");
        (await Lote(a, Debito(canal, seq: 2, delta: -14, depois: 50))).Result.ShouldBe("applied");
        (await PostAsync<CloudReleaseReply>(a, "/api/cloud/v1/leases/release",
            new CloudReleaseRequest(Jogador, 1, 2))).Released.ShouldBeTrue();

        var b = await Acquire(env.Cliente(env.ChaveB));
        b.Status.ShouldBe("granted");
        var item = b.Channels.Single().Items.Single();
        item.Fingerprint.ShouldBe(Diamante);
        item.Amount.ShouldBe(50);
        Convert.FromBase64String(item.Encoded).ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Reenvio_do_mesmo_lote_e_duplicado_e_nao_aplica_duas_vezes()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        var lote = Credito(canal, seq: 1, delta: 10, depois: 10);

        (await Lote(a, lote)).Result.ShouldBe("applied");
        (await Lote(a, lote)).Result.ShouldBe("duplicate");

        (await env.SaldoAsync(Diamante)).ShouldBe(10);
        (await env.ContarLedgerAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Mesmo_numero_com_conteudo_diferente_e_quarentena()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 10, depois: 10));

        var outro = await Lote(a, Credito(canal, seq: 1, delta: 99, depois: 99));

        outro.Result.ShouldBe("quarantined");
        (await env.SaldoAsync(Diamante)).ShouldBe(10);
    }

    [Fact]
    public async Task Debito_alem_do_saldo_vai_para_quarentena_e_congela_o_canal()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 5, depois: 5));

        var r = await Lote(a, Debito(canal, seq: 2, delta: -6, depois: -1));

        r.Result.ShouldBe("quarantined");
        r.Reason.ShouldBe(nameof(CloudQuarantineReason.NegativeBalance));
        (await env.SaldoAsync(Diamante)).ShouldBe(5);
        (await env.CanalCongeladoAsync(canal)).ShouldBeTrue();
        (await Acquire(a)).ReadOnly.ShouldBeTrue("canal congelado volta como somente leitura");
    }

    [Fact]
    public async Task Lote_de_servidor_que_perdeu_o_lease_vai_para_quarentena()
    {
        // O cenário que o lease existe para impedir: A guardou no diário, caiu,
        // o lease venceu, B tomou e mexeu. O lote atrasado de A não pode
        // sobrescrever o que B fez.
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        env.Relogio.Avancar(TimeSpan.FromMinutes(31));
        await Acquire(env.Cliente(env.ChaveB));

        var atrasado = await Lote(a, Credito(canal, seq: 1, delta: 10, depois: 10));

        atrasado.Result.ShouldBe("quarantined");
        atrasado.Reason.ShouldBe(nameof(CloudQuarantineReason.StaleEpoch));
        (await env.SaldoAsync(Diamante)).ShouldBe(0);
    }

    [Fact]
    public async Task Release_com_lote_faltando_nao_libera()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 1, depois: 1));

        (await PostAsync<CloudReleaseReply>(a, "/api/cloud/v1/leases/release",
            new CloudReleaseRequest(Jogador, 1, LastSeq: 2))).Released.ShouldBeFalse();
        (await Acquire(env.Cliente(env.ChaveB))).Status.ShouldBe("busy");
    }

    [Fact]
    public async Task Corpo_sem_os_campos_obrigatorios_e_400()
    {
        await using var env = await Ambiente.CriarAsync();

        var resposta = await env.Cliente(env.ChaveA)
            .PostAsJsonAsync("/api/cloud/v1/batches", new { playerUuid = Jogador }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- governança (fatia D1)

    [Fact]
    public async Task Mundo_que_voltou_no_tempo_abre_incidente_e_trava_a_nuvem_naquele_servidor()
    {
        await using var env = await Ambiente.CriarAsync();
        var mundo = Guid.CreateVersion7();
        var a = env.Cliente(env.ChaveA);
        await PostAsync<CloudHelloReply>(a, "/api/cloud/v1/hello", HelloDoMundo(mundo, null));
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 64, depois: 64));

        // Reinício (chave nova) com o mundo restaurado de antes do lote 1.
        var reiniciado = env.Cliente(await env.RotacionarChaveAAsync());
        var hello = await PostAsync<CloudHelloReply>(reiniciado, "/api/cloud/v1/hello", HelloDoMundo(mundo, []));

        hello.ReadOnly.ShouldBeTrue();
        hello.ReadOnlyReason!.ShouldContain("voltou no tempo");
        (await env.ContarAsync(db => db.CloudRollbackIncidents)).ShouldBe(1);

        // Novo boot com o incidente ainda aberto: continua travado, sem abrir outro.
        (await PostAsync<CloudHelloReply>(reiniciado, "/api/cloud/v1/hello", HelloDoMundo(mundo, []))).ReadOnly.ShouldBeTrue();
        (await env.ContarAsync(db => db.CloudRollbackIncidents)).ShouldBe(1);
    }

    [Fact]
    public async Task Mapa_novo_nao_e_rollback()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        await PostAsync<CloudHelloReply>(a, "/api/cloud/v1/hello", HelloDoMundo(Guid.CreateVersion7(), null));
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 1, depois: 1));

        var reiniciado = env.Cliente(await env.RotacionarChaveAAsync());
        var hello = await PostAsync<CloudHelloReply>(reiniciado, "/api/cloud/v1/hello",
            HelloDoMundo(Guid.CreateVersion7(), []));

        hello.ReadOnly.ShouldBeFalse();
        (await env.ContarAsync(db => db.CloudRollbackIncidents)).ShouldBe(0);
    }

    [Fact]
    public async Task Regras_chegam_no_hello_e_o_heartbeat_avisa_a_versao()
    {
        await using var env = await Ambiente.CriarAsync();
        await env.CriarRegraAsync(CloudRuleScope.Mod, "refinedstorage", CloudRuleAction.Block);
        var a = env.Cliente(env.ChaveA);

        var hello = await PostAsync<CloudHelloReply>(a, "/api/cloud/v1/hello", Hello());
        hello.Rules.Single().ShouldBe(new CloudRuleDto("Mod", "refinedstorage", "Block"));

        await Acquire(a);
        var hb = await PostAsync<CloudHeartbeatReply>(a, "/api/cloud/v1/leases/heartbeat",
            new CloudHeartbeatRequest([new CloudHeldLeaseDto(Jogador, 1)]));
        hb.PolicyVersion.ShouldBe(hello.PolicyVersion);

        var policy = await PostAsync<CloudPolicyReply>(a, "/api/cloud/v1/policy", new { });
        policy.Rules.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Suspeitos_somam_tentativas_e_duvidas_nao_duplicam_no_reenvio()
    {
        await using var env = await Ambiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var suspeito = new CloudSuspectsRequest([new CloudSuspectDto("SophisticatedBackpacks:Backpack", "storage_uuid", 2)]);
        await PostOkAsync(a, "/api/cloud/v1/reports/suspects", suspeito);
        await PostOkAsync(a, "/api/cloud/v1/reports/suspects", suspeito);

        var duvida = new CloudDoubtfulRequest("boot-1",
            [new CloudDoubtfulDto(Jogador, Guid.CreateVersion7(), Diamante, "RecentDebit", 3)]);
        await PostOkAsync(a, "/api/cloud/v1/reports/doubtful", duvida);
        await PostOkAsync(a, "/api/cloud/v1/reports/doubtful", duvida);

        await using var db = await env.DbAsync();
        var s = await db.CloudSuspectItems.SingleAsync(Ct);
        s.ItemId.ShouldBe("sophisticatedbackpacks:backpack");
        s.Attempts.ShouldBe(4);
        (await db.CloudDoubtfulOperations.CountAsync(Ct)).ShouldBe(1);
    }

    // ---------------------------------------------------------------- ajudantes

    private static CloudHelloRequest HelloDoMundo(Guid mundo, Dictionary<string, CloudSeqPosition>? jogadores) =>
        new(CloudProtocol.Current, "0.1.0", new CloudCheckpoint(mundo, jogadores));

    private static async Task PostOkAsync(HttpClient client, string url, object body)
    {
        var resposta = await client.PostAsJsonAsync(url, body, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
    }

    private static CloudHelloRequest Hello() =>
        new(CloudProtocol.Current, "0.1.0", new CloudCheckpoint(Guid.CreateVersion7(), null));

    private static async Task<CloudAcquireReply> Acquire(HttpClient client) =>
        await PostAsync<CloudAcquireReply>(client, "/api/cloud/v1/leases/acquire",
            new CloudAcquireRequest(Jogador, "ana"));

    private static async Task<CloudBatchReply> Lote(HttpClient client, CloudBatchRequest lote) =>
        await PostAsync<CloudBatchReply>(client, "/api/cloud/v1/batches", lote);

    private static CloudBatchRequest Credito(Guid canal, long seq, long delta, long depois) =>
        new(Jogador, 1, seq, [new CloudOpDto(canal, Diamante, delta)], [new CloudExpectedDto(canal, Diamante, depois)],
            [new CloudItemDto(Diamante, "minecraft:diamond", "Diamante", Convert.ToBase64String([1, 2, 3]))]);

    private static CloudBatchRequest Debito(Guid canal, long seq, long delta, long depois) =>
        new(Jogador, 1, seq, [new CloudOpDto(canal, Diamante, delta)], [new CloudExpectedDto(canal, Diamante, depois)], null);

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var resposta = await client.PostAsJsonAsync(url, body, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Relógio controlado: o lease expira por tempo, e o teste não pode esperar 30 minutos.</summary>
    private sealed class RelogioFalso : TimeProvider
    {
        private DateTimeOffset _agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }

    /// <summary>Uma nuvem com dois servidores do mesmo dono (A e B), cada um com sua chave.</summary>
    private sealed class Ambiente : IAsyncDisposable
    {
        public required TcMineAppFactory Factory { get; init; }
        public required RelogioFalso Relogio { get; init; }
        public required Guid ServidorA { get; init; }
        public required Guid Nuvem { get; init; }
        public required string ChaveA { get; init; }
        public required string ChaveB { get; init; }

        public static async Task<Ambiente> CriarAsync()
        {
            var relogio = new RelogioFalso();
            var factory = new TcMineAppFactory { Servicos = s => s.AddSingleton<TimeProvider>(relogio) };
            factory.CreateClient(); // sobe o host: as migrations rodam no arranque

            var dbs = factory.Services.GetRequiredService<IDbContextFactory<TcMineDbContext>>();
            await using var db = await dbs.CreateDbContextAsync(Ct);
            var dono = Guid.CreateVersion7();
            var nuvem = new CloudVault { Name = "Nuvem", OwnerId = dono };
            db.CloudVaults.Add(nuvem);
            var (a, chaveA) = NovoServidor("Servidor A", dono, nuvem);
            var (b, chaveB) = NovoServidor("Servidor B", dono, nuvem);
            db.GameServers.AddRange(a, b);
            await db.SaveChangesAsync(Ct);
            db.CloudServerCredentials.AddRange(Credencial(a, nuvem, chaveA), Credencial(b, nuvem, chaveB));
            await db.SaveChangesAsync(Ct);

            return new Ambiente
            {
                Factory = factory, Relogio = relogio, ServidorA = a.Id, Nuvem = nuvem.Id,
                ChaveA = chaveA.Key, ChaveB = chaveB.Key
            };
        }

        private static (GameServer, (string Key, string Prefix, string Hash)) NovoServidor(string nome, Guid dono,
            CloudVault nuvem)
        {
            var servidor = new GameServer
            {
                Name = nome, ModpackId = Guid.CreateVersion7(), ModpackVersionId = Guid.CreateVersion7(),
                ConnectAddress = "localhost", RconSecret = "segredo", OwnerId = dono
            };
            servidor.AttachToCloudVault(nuvem);
            return (servidor, CloudServerKey.Generate());
        }

        private static CloudServerCredential Credencial(GameServer servidor, CloudVault nuvem,
            (string Key, string Prefix, string Hash) chave) => new()
        {
            GameServerId = servidor.Id, VaultId = nuvem.Id, KeyPrefix = chave.Prefix, KeyHash = chave.Hash
        };

        public HttpClient Cliente(string chave)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chave);
            return client;
        }

        public async Task AlterarServidorAsync(Guid id, Action<GameServer> mudanca)
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            var servidor = await db.GameServers.SingleAsync(s => s.Id == id, Ct);
            mudanca(servidor);
            await db.SaveChangesAsync(Ct);
        }

        /// <summary>Simula um reinício do servidor A: chave nova, a anterior revogada (ProvisionServerCloudKey).</summary>
        public async Task<string> RotacionarChaveAAsync()
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            foreach (var antiga in await db.CloudServerCredentials.Where(c => c.GameServerId == ServidorA).ToListAsync(Ct))
                antiga.Revoke(Relogio.GetUtcNow());
            var (key, prefix, hash) = CloudServerKey.Generate();
            db.CloudServerCredentials.Add(new CloudServerCredential
                { GameServerId = ServidorA, VaultId = Nuvem, KeyPrefix = prefix, KeyHash = hash });
            await db.SaveChangesAsync(Ct);
            return key;
        }

        public async Task CriarRegraAsync(CloudRuleScope scope, string pattern, CloudRuleAction action)
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            db.CloudItemRules.Add(new CloudItemRule { VaultId = Nuvem, Scope = scope, Pattern = pattern, Action = action });
            await db.SaveChangesAsync(Ct);
        }

        public async Task<int> ContarAsync<T>(Func<TcMineDbContext, IQueryable<T>> tabela)
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            return await tabela(db).CountAsync(Ct);
        }

        public Task<TcMineDbContext> DbAsync() => Dbs().CreateDbContextAsync(Ct);

        public async Task<long> SaldoAsync(string fingerprint)
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            return await (from b in db.CloudBalances
                          join i in db.CloudItemTypes on b.ItemTypeId equals i.Id
                          where i.Fingerprint == fingerprint
                          select b.Amount).SumAsync(Ct);
        }

        public async Task<int> ContarLedgerAsync()
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            return await db.CloudLedger.CountAsync(Ct);
        }

        public async Task<bool> CanalCongeladoAsync(Guid canal)
        {
            await using var db = await Dbs().CreateDbContextAsync(Ct);
            return (await db.CloudChannels.SingleAsync(c => c.Id == canal, Ct)).IsFrozen;
        }

        private IDbContextFactory<TcMineDbContext> Dbs() =>
            Factory.Services.GetRequiredService<IDbContextFactory<TcMineDbContext>>();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
