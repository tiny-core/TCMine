using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O apagar da retenção (5.4) contra um banco de verdade. As regras de prazo
///     estão no caso de uso (RunRetentionTests); aqui o que importa é o SQL:
///     corte pelo Id, ordem ledger → lote (a FK do ledger impediria o contrário),
///     e o que NUNCA sai — decisões do dono e o que é de outro servidor ou nuvem.
///     As linhas são todas criadas agora, então "antes de amanhã" pega todas e
///     "antes de ontem" não pega nenhuma.
/// </summary>
public sealed class RetentionRepositoryTests
{
    private const string Ana = "069a79f444e94726a5befca90e38aaf5";

    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateTimeOffset Amanha => DateTimeOffset.UtcNow.AddDays(1);
    private static DateTimeOffset Ontem => DateTimeOffset.UtcNow.AddDays(-1);

    [Fact]
    public async Task Historico_de_jogo_sai_e_o_que_e_decisao_do_dono_fica_no_sqlite()
    {
        using var factory = new SqliteTestFactory();
        await HistoricoDeJogo(factory);
    }

    [Fact]
    public async Task Historico_de_jogo_sai_e_o_que_e_decisao_do_dono_fica_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await HistoricoDeJogo(postgres);
    }

    [Fact]
    public async Task Servidores_com_historico_antigo_vem_com_incidente_aberto_e_backup_mais_antigo()
    {
        using var factory = new SqliteTestFactory();
        var c = await Semear(factory);
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            db.Add(Lote(c.Nuvem, c.ServidorA, 1));
            // Mesmo jogador nos dois servidores: a sequência é da nuvem, não do
            // servidor (índice único VaultId, PlayerUuid, Epoch, Seq), então não repete.
            db.Add(Lote(c.Nuvem, c.ServidorB, 2));
            db.Add(new CloudRollbackIncident
            {
                VaultId = c.Nuvem, ServerId = c.ServidorA, WorldId = Guid.CreateVersion7(),
                CheckpointJson = "{}", Detail = "teste"
            });
            db.AddRange(Backup(c.ServidorB, "a.zip"), Backup(c.ServidorB, "b.zip"));
            await db.SaveChangesAsync(Ct);
        }

        var repo = new RetentionRepository(factory);
        var servidores = (await repo.ListServersWithGameHistoryAsync(c.Nuvem, Amanha, Ct))
            .ToDictionary(s => s.ServerId);

        servidores[c.ServidorA].HasOpenIncident.ShouldBeTrue();
        servidores[c.ServidorA].OldestBackupAt.ShouldBeNull();
        servidores[c.ServidorB].HasOpenIncident.ShouldBeFalse();
        servidores[c.ServidorB].OldestBackupAt.ShouldNotBeNull();
        (await repo.ListServersWithGameHistoryAsync(c.Nuvem, Ontem, Ct)).ShouldBeEmpty("nada é antigo ainda");
    }

    [Fact]
    public async Task Feed_convites_mortos_e_pedidos_resolvidos_saem_e_o_que_vale_fica()
    {
        using var factory = new SqliteTestFactory();
        var c = await Semear(factory);
        var agora = DateTimeOffset.UtcNow;
        Guid valido, pendente;
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            db.AddRange(
                new ActivityEvent { Kind = ActivityEventKind.UserLoggedIn, Message = "a" },
                new ActivityEvent { Kind = ActivityEventKind.UserLoggedIn, Message = "b" });
            var ok = Convite(c.ServidorA, agora.AddDays(30), 'a');
            var morto = Convite(c.ServidorA, agora.AddDays(-1), 'b');
            var espera = new AccessRequest { UserId = Guid.CreateVersion7(), GameServerId = c.ServidorA };
            var recusa = new AccessRequest { UserId = Guid.CreateVersion7(), GameServerId = c.ServidorA };
            recusa.Deny(Guid.CreateVersion7(), agora);
            db.AddRange(ok, morto, espera, recusa);
            await db.SaveChangesAsync(Ct);
            (valido, pendente) = (ok.Id, espera.Id);
        }

        var repo = new RetentionRepository(factory);
        (await repo.PurgeActivityAsync(Ontem, Ct)).ShouldBe(0);
        (await repo.PurgeActivityAsync(Amanha, Ct)).ShouldBe(2);
        var limpeza = await repo.PurgeHousekeepingAsync(Amanha, agora, Ct);

        limpeza.Invites.ShouldBe(1);
        limpeza.AccessRequests.ShouldBe(1);
        await using var depois = await factory.CreateDbContextAsync(Ct);
        (await depois.Invites.Select(i => i.Id).ToListAsync(Ct)).ShouldBe([valido], "convite que ainda vale fica");
        (await depois.AccessRequests.Select(r => r.Id).ToListAsync(Ct)).ShouldBe([pendente],
            "pedido pendente nunca sai");
    }

    private static async Task HistoricoDeJogo(IDbContextFactory<TcMineDbContext> factory)
    {
        var c = await Semear(factory);
        Guid deQuarentena, estornado, doOutroServidor, deOutraNuvem;
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            // Lote comum do A: as duas linhas de jogo e o lote saem.
            var comum = Lote(c.Nuvem, c.ServidorA, 1);
            // Lote aplicado pelo dono numa quarentena: a linha do painel e a
            // quarentena apontam para ele, então ele e elas ficam.
            var quarentenado = Lote(c.Nuvem, c.ServidorA, 2);
            quarentenado.MarkQuarantined();
            quarentenado.MarkResolved(CloudBatchStatus.Applied);
            // Estornado num incidente: a linha de jogo sai, o lote fica.
            var revertido = Lote(c.Nuvem, c.ServidorA, 3);
            revertido.MarkReverted();
            // A sequência é da nuvem (índice único VaultId, PlayerUuid, Epoch, Seq):
            // o lote do outro servidor continua a numeração do jogador.
            var outroServidor = Lote(c.Nuvem, c.ServidorB, 4);
            var outraNuvem = Lote(c.OutraNuvem, c.ServidorA, 1);
            db.AddRange(comum, quarentenado, revertido, outroServidor, outraNuvem);
            db.Add(new CloudQuarantine
            {
                VaultId = c.Nuvem, BatchId = quarentenado.Id, Reason = CloudQuarantineReason.StaleEpoch, Detail = "teste", PayloadJson = "{}"
            });
            db.AddRange(
                Linha(c.Nuvem, c.Canal, c.Item, comum.Id, CloudLedgerSource.Game),
                Linha(c.Nuvem, c.Canal, c.Item, comum.Id, CloudLedgerSource.Game),
                Linha(c.Nuvem, c.Canal, c.Item, quarentenado.Id, CloudLedgerSource.QuarantineApply),
                Linha(c.Nuvem, c.Canal, c.Item, revertido.Id, CloudLedgerSource.Game),
                Linha(c.Nuvem, c.Canal, c.Item, null, CloudLedgerSource.Revert),
                Linha(c.Nuvem, c.Canal, c.Item, null, CloudLedgerSource.Admin),
                Linha(c.Nuvem, c.Canal, c.Item, outroServidor.Id, CloudLedgerSource.Game),
                Linha(c.OutraNuvem, c.CanalOutraNuvem, c.Item, outraNuvem.Id, CloudLedgerSource.Game));
            await db.SaveChangesAsync(Ct);
            (deQuarentena, estornado, doOutroServidor, deOutraNuvem) =
                (quarentenado.Id, revertido.Id, outroServidor.Id, outraNuvem.Id);
        }

        var repo = new RetentionRepository(factory);
        (await repo.PurgeServerGameHistoryAsync(c.Nuvem, c.ServidorA, Ontem, Ct))
            .ShouldBe(new(0, 0), "o corte é pelo Id: nada foi criado antes de ontem");

        var apagado = await repo.PurgeServerGameHistoryAsync(c.Nuvem, c.ServidorA, Amanha, Ct);

        apagado.ShouldBe(new(3, 1));
        await using var depois = await factory.CreateDbContextAsync(Ct);
        (await depois.CloudBatches.Select(b => b.Id).ToListAsync(Ct))
            .ShouldBe([deQuarentena, estornado, doOutroServidor, deOutraNuvem], ignoreOrder: true);
        var fontes = await depois.CloudLedger.Where(e => e.VaultId == c.Nuvem).Select(e => e.Source)
            .ToListAsync(Ct);
        fontes.ShouldBe(
            [CloudLedgerSource.QuarantineApply, CloudLedgerSource.Revert, CloudLedgerSource.Admin,
                CloudLedgerSource.Game], ignoreOrder: true);
        (await depois.CloudLedger.CountAsync(e => e.VaultId == c.OutraNuvem, Ct)).ShouldBe(1);
        (await depois.CloudQuarantine.CountAsync(Ct)).ShouldBe(1);
    }

    private static async Task<Cenario> Semear(IDbContextFactory<TcMineDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync(Ct);
        var dono = Guid.CreateVersion7();
        var nuvem = new CloudVault { Name = "N", OwnerId = dono };
        var outra = new CloudVault { Name = "O", OwnerId = dono };
        var a = Servidor("A", 25565, dono);
        var b = Servidor("B", 25566, dono);
        var canal = new CloudChannel { VaultId = nuvem.Id, PlayerUuid = Ana, Name = "Principal" };
        var canalOutra = new CloudChannel { VaultId = outra.Id, PlayerUuid = Ana, Name = "Principal" };
        var item = new CloudItemType
        {
            Fingerprint = new string('d', 64), ItemId = "minecraft:diamond", ModId = "minecraft",
            DisplayName = "Diamante", Encoded = [1]
        };
        db.AddRange(nuvem, outra, a, b, canal, canalOutra, item);
        await db.SaveChangesAsync(Ct);
        return new Cenario(nuvem.Id, outra.Id, a.Id, b.Id, canal.Id, canalOutra.Id, item.Id);
    }

    private static GameServer Servidor(string nome, int porta, Guid dono) => new()
    {
        Name = nome,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "localhost",
        RconSecret = "segredo",
        OwnerId = dono,
        GamePort = porta
    };

    private static CloudBatch Lote(Guid nuvem, Guid servidor, long seq) => new()
    {
        VaultId = nuvem, ServerId = servidor, PlayerUuid = Ana, Epoch = 1, Seq = seq,
        PayloadHash = new string('a', 64)
    };

    private static CloudLedgerEntry Linha(Guid nuvem, Guid canal, Guid item, Guid? lote, CloudLedgerSource origem) =>
        new()
        {
            VaultId = nuvem, ChannelId = canal, ItemTypeId = item, Delta = 1, BalanceAfter = 1, Source = origem,
            BatchId = lote
        };

    private static WorldBackup Backup(Guid servidor, string arquivo) => new()
    {
        GameServerId = servidor, FileName = arquivo, SizeBytes = 1, Reason = WorldBackupReason.Manual
    };

    private static Invite Convite(Guid servidor, DateTimeOffset expira, char c) => new()
    {
        CodeHash = new string(c, 64), GameServerId = servidor, Role = ServerRole.Member,
        CreatedByUserId = Guid.CreateVersion7(), ExpiresAt = expira
    };

    private sealed record Cenario(
        Guid Nuvem,
        Guid OutraNuvem,
        Guid ServidorA,
        Guid ServidorB,
        Guid Canal,
        Guid CanalOutraNuvem,
        Guid Item);
}
