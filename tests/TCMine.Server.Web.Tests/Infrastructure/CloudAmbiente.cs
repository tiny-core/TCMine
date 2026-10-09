using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Web.Tests.Infrastructure;

// Ambiente dos testes da nuvem: a aplicação inteira em memória, uma nuvem com
// dois servidores do mesmo dono (A e B), cada um com sua chave, e o relógio
// controlado. Compartilhado pelos testes da API (CloudApiContractTests) e das
// decisões do painel (CloudGovernanceFlowTests).

/// <summary>Relógio controlado: o lease expira por tempo, e o teste não pode esperar 30 minutos.</summary>
internal sealed class RelogioFalso : TimeProvider
{
    private DateTimeOffset _agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan quanto) => _agora += quanto;
}

/// <summary>Uma nuvem com dois servidores do mesmo dono (A e B), cada um com sua chave.</summary>
internal sealed class CloudAmbiente : IAsyncDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Dono da nuvem e dos dois servidores; é também quem usa o painel nos testes.</summary>
    public required Guid Dono { get; init; }

    public required TcMineAppFactory Factory { get; init; }
    public required RelogioFalso Relogio { get; init; }
    public required Guid ServidorA { get; init; }
    public required Guid Nuvem { get; init; }
    public required string ChaveA { get; init; }
    public required string ChaveB { get; init; }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();

    public static async Task<CloudAmbiente> CriarAsync()
    {
        var relogio = new RelogioFalso();
        var dono = Guid.CreateVersion7();
        var factory = new TcMineAppFactory
        {
            Servicos = s =>
            {
                s.AddSingleton<TimeProvider>(relogio);
                // O painel lê o usuário da requisição HTTP; aqui os casos de uso são
                // chamados direto, como o dono da nuvem.
                s.AddScoped<ICurrentUserScope>(_ => new DonoDoPainel(dono));
            }
        };
        factory.CreateClient(); // sobe o host: as migrations rodam no arranque

        var dbs = factory.Services.GetRequiredService<IDbContextFactory<TcMineDbContext>>();
        await using var db = await dbs.CreateDbContextAsync(Ct);
        var nuvem = new CloudVault { Name = "Nuvem", OwnerId = dono };
        db.CloudVaults.Add(nuvem);
        // Portas diferentes: o índice único de GamePort recusa dois servidores
        // na mesma, que é justamente o que o padrão daria aos dois.
        var (a, chaveA) = NovoServidor("Servidor A", dono, nuvem, 25565);
        var (b, chaveB) = NovoServidor("Servidor B", dono, nuvem, 25566);
        db.GameServers.AddRange(a, b);
        await db.SaveChangesAsync(Ct);
        db.CloudServerCredentials.AddRange(Credencial(a, nuvem, chaveA), Credencial(b, nuvem, chaveB));
        await db.SaveChangesAsync(Ct);

        return new CloudAmbiente
        {
            Factory = factory,
            Relogio = relogio,
            Dono = dono,
            ServidorA = a.Id,
            Nuvem = nuvem.Id,
            ChaveA = chaveA.Key,
            ChaveB = chaveB.Key
        };
    }

    private static (GameServer, (string Key, string Prefix, string Hash)) NovoServidor(string nome, Guid dono,
        CloudVault nuvem, int porta)
    {
        var servidor = new GameServer
        {
            Name = nome,
            ModpackId = Guid.CreateVersion7(),
            ModpackVersionId = Guid.CreateVersion7(),
            ConnectAddress = "localhost",
            GamePort = porta,
            RconSecret = "segredo",
            OwnerId = dono
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
        {
            GameServerId = ServidorA, VaultId = Nuvem, KeyPrefix = prefix, KeyHash = hash
        });
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

    /// <summary>Resolve um caso de uso do painel num escopo novo (como uma requisição) e o executa.</summary>
    public async Task<T> PainelAsync<TUseCase, T>(Func<TUseCase, Task<T>> acao) where TUseCase : notnull
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await acao(scope.ServiceProvider.GetRequiredService<TUseCase>());
    }

    private sealed class DonoDoPainel(Guid dono) : ICurrentUserScope
    {
        public Guid? UserId => dono;
        public Guid OwnerId => dono;
        public bool IsInstanceAdmin => false;

        public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult<ServerRoleDto?>(ServerRoleDto.Owner);

        public Task<ModpackRoleDto?> GetModpackRoleAsync(Guid modpackId, CancellationToken ct) =>
            Task.FromResult<ModpackRoleDto?>(ModpackRoleDto.Owner);
    }
}

/// <summary>Chamadas da API da nuvem como o mod faz, para os testes.</summary>
internal static class CloudApi
{
    public const string Jogador = "069a79f444e94726a5befca90e38aaf5";
    public static readonly string Diamante = new('d', 64);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static CloudHelloRequest HelloDoMundo(Guid mundo, Dictionary<string, CloudSeqPosition>? jogadores) =>
        new(CloudProtocol.Current, "0.1.0", new CloudCheckpoint(mundo, jogadores));

    public static async Task PostOkAsync(HttpClient client, string url, object body)
    {
        var resposta = await client.PostAsJsonAsync(url, body, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
    }

    public static CloudHelloRequest Hello() =>
        new(CloudProtocol.Current, "0.1.0", new CloudCheckpoint(Guid.CreateVersion7(), null));

    public static async Task<CloudAcquireReply> Acquire(HttpClient client) =>
        await PostAsync<CloudAcquireReply>(client, "/api/cloud/v1/leases/acquire",
            new CloudAcquireRequest(Jogador, "ana"));

    public static async Task<CloudBatchReply> Lote(HttpClient client, CloudBatchRequest lote) =>
        await PostAsync<CloudBatchReply>(client, "/api/cloud/v1/batches", lote);

    public static CloudBatchRequest Credito(Guid canal, long seq, long delta, long depois) =>
        new(Jogador, 1, seq, [new CloudOpDto(canal, Diamante, delta)], [new CloudExpectedDto(canal, Diamante, depois)],
            [new CloudItemDto(Diamante, "minecraft:diamond", "Diamante", Convert.ToBase64String([1, 2, 3]))]);

    public static CloudBatchRequest Debito(Guid canal, long seq, long delta, long depois) =>
        new(Jogador, 1, seq, [new CloudOpDto(canal, Diamante, delta)], [new CloudExpectedDto(canal, Diamante, depois)],
            null);

    public static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var resposta = await client.PostAsJsonAsync(url, body, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
