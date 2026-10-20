using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     As filas de decisão da nuvem (fase 6.4): quarentena, em dúvida e
///     incidentes paginam por cursor no banco, os suspeitos vêm ordenados e
///     cortados no banco, e os selos das abas são contados no banco. Antes cada
///     lista trazia a tabela inteira e ordenava em memória, e o que já foi
///     decidido nunca expira.
/// </summary>
public sealed class CloudDecisionListsTests
{
    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private const string Jogador = "0123456789abcdef0123456789abcdef";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filas_paginam_ordenam_e_contam_no_banco()
    {
        using var factory = new SqliteTestFactory();
        await FilasNoBanco(factory);
    }

    [Fact]
    public async Task Filas_paginam_ordenam_e_contam_no_banco_no_postgres()
    {
        // Cursor pelo Id (comparação de GUID) e o join da quarentena: é na
        // tradução de cada banco que a página poderia repetir ou pular linha.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await FilasNoBanco(postgres);
    }

    private static async Task FilasNoBanco(IDbContextFactory<TcMineDbContext> factory)
    {
        var nuvem = new CloudVault { Name = "Nuvem", OwnerId = Guid.CreateVersion7() };
        var outra = new CloudVault { Name = "Outra", OwnerId = Guid.CreateVersion7() };
        var servidor = Guid.CreateVersion7();
        var agora = DateTimeOffset.UtcNow;
        var dono = Guid.CreateVersion7();

        var incidentes = Enumerable.Range(0, 5).Select(_ => Incidente(nuvem.Id, servidor)).ToList();
        incidentes[0].Resolve(CloudIncidentStatus.Accepted, dono, agora);

        var lotes = Enumerable.Range(1, 3).Select(seq => Lote(nuvem.Id, servidor, seq)).ToList();
        var quarentena = lotes.Select(Quarentena).ToList();
        quarentena[0].Resolve(CloudQuarantineResolution.Discarded, dono, agora);

        var duvidas = Enumerable.Range(0, 3).Select(i => Duvida(nuvem.Id, servidor, i)).ToList();
        duvidas[0].Resolve(CloudDoubtfulStatus.Dismissed, dono, agora);

        var suspeitos = new[] { Suspeito(nuvem.Id, "a:pouco", 1), Suspeito(nuvem.Id, "a:muito", 9),
            Suspeito(nuvem.Id, "a:medio", 4) };

        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            db.CloudVaults.AddRange(nuvem, outra);
            db.CloudRollbackIncidents.AddRange(incidentes);
            db.CloudRollbackIncidents.Add(Incidente(outra.Id, servidor)); // de outra nuvem, nunca aparece
            db.CloudBatches.AddRange(lotes);
            db.CloudQuarantine.AddRange(quarentena);
            db.CloudDoubtfulOperations.AddRange(duvidas);
            db.CloudSuspectItems.AddRange(suspeitos);
            await db.SaveChangesAsync(Ct);
        }

        var repo = new CloudGovernanceRepository(factory);

        // Incidentes, todos: 5 em páginas de 2 = 3 páginas, do mais novo ao
        // mais velho na ordem do banco, sem repetir nem pular.
        var vistos = new List<Guid>();
        Guid? cursor = null;
        var paginas = 0;
        do
        {
            var pagina = await repo.ListIncidentsAsync(nuvem.Id, false, cursor, 2, Ct);
            pagina.Items.Count.ShouldBeLessThanOrEqualTo(2, "nenhuma página passa do tamanho pedido");
            vistos.AddRange(pagina.Items.Select(i => i.Id));
            cursor = pagina.Next;
            paginas++;
        } while (cursor is not null && paginas < 10);

        paginas.ShouldBe(3);
        vistos.ShouldBe(
            incidentes.Select(i => i.Id).OrderByDescending(DatabaseGuidOrder.Key, StringComparer.Ordinal),
            ignoreOrder: false);
        (await repo.ListIncidentsAsync(nuvem.Id, true, null, 50, Ct)).Items.Count.ShouldBe(4, "só os abertos");

        // Quarentena: o join com o lote continua de pé na página.
        var q1 = await repo.ListQuarantineAsync(nuvem.Id, false, null, 2, Ct);
        q1.Items.Count.ShouldBe(2);
        q1.Next.ShouldNotBeNull();
        var q2 = await repo.ListQuarantineAsync(nuvem.Id, false, q1.Next, 2, Ct);
        q2.Next.ShouldBeNull();
        q1.Items.Concat(q2.Items).Select(v => v.Quarantine.Id)
            .ShouldBe(quarentena.Select(q => q.Id), ignoreOrder: true);
        q1.Items.Concat(q2.Items).ShouldAllBe(v => v.Batch.Id == v.Quarantine.BatchId);
        (await repo.ListQuarantineAsync(nuvem.Id, true, null, 50, Ct)).Items.Count.ShouldBe(2);

        var d = await repo.ListDoubtfulAsync(nuvem.Id, true, null, 50, Ct);
        d.Items.Count.ShouldBe(2);
        d.Next.ShouldBeNull();

        // Suspeitos: os mais tentados primeiro, cortados no banco.
        (await repo.ListSuspectsAsync(nuvem.Id, CloudSuspectStatus.Pending, 2, Ct))
            .Select(s => s.ItemId).ShouldBe(["a:muito", "a:medio"]);

        // Selos das abas: o que espera decisão, contado no banco.
        (await repo.CountOpenAsync(nuvem.Id, Ct))
            .ShouldBe(new CloudOpenCounts(Suspects: 3, Quarantine: 2, Doubtful: 2, Incidents: 4));
        (await repo.CountOpenAsync(outra.Id, Ct)).ShouldBe(new CloudOpenCounts(0, 0, 0, 1));
    }

    private static CloudRollbackIncident Incidente(Guid nuvem, Guid servidor) => new()
    {
        VaultId = nuvem, ServerId = servidor, WorldId = Guid.CreateVersion7(), CheckpointJson = "{}",
        Detail = "teste"
    };

    private static CloudBatch Lote(Guid nuvem, Guid servidor, long seq) => new()
    {
        VaultId = nuvem, ServerId = servidor, PlayerUuid = Jogador, Epoch = 1, Seq = seq,
        PayloadHash = new string('a', 64)
    };

    private static CloudQuarantine Quarentena(CloudBatch lote) => new()
    {
        VaultId = lote.VaultId, BatchId = lote.Id, Reason = CloudQuarantineReason.StaleEpoch, Detail = "teste",
        PayloadJson = "{}"
    };

    private static CloudDoubtfulOperation Duvida(Guid nuvem, Guid servidor, int indice) => new()
    {
        VaultId = nuvem, ServerId = servidor, ReportId = "relatorio", Index = indice, PlayerUuid = Jogador,
        ChannelId = Guid.CreateVersion7(), Fingerprint = new string('f', CloudItemType.FingerprintLength),
        Kind = CloudDoubtfulKind.PendingCredit, Amount = 1
    };

    private static CloudSuspectItem Suspeito(Guid nuvem, string item, long tentativas)
    {
        var suspeito = new CloudSuspectItem { VaultId = nuvem, ItemId = item };
        suspeito.Seen(tentativas, "teste", DateTimeOffset.UtcNow);
        return suspeito;
    }
}
