using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     As garantias da nuvem que moram no BANCO, não no código: índice único do
///     lote (o mesmo lote nunca entra duas vezes) e o token de concorrência do
///     lease (dois lotes ao mesmo tempo, um perde). Se alguém tirar a
///     configuração, é aqui que quebra — o código continuaria compilando.
/// </summary>
public sealed class CloudPersistenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lote_com_mesma_epoca_e_seq_nao_entra_duas_vezes()
    {
        using var factory = new SqliteTestFactory();
        var vault = await NovaNuvem(factory);

        await using (var db = factory.CreateDbContext())
        {
            db.CloudBatches.Add(Lote(vault.Id, 1));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = factory.CreateDbContext())
        {
            db.CloudBatches.Add(Lote(vault.Id, 1));
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task Dois_lotes_ao_mesmo_tempo_no_mesmo_lease_um_perde()
    {
        using var factory = new SqliteTestFactory();
        var vault = await NovaNuvem(factory);
        var servidor = Guid.CreateVersion7();
        await using (var db = factory.CreateDbContext())
        {
            var lease = new CloudLease { VaultId = vault.Id, PlayerUuid = new string('a', 32) };
            lease.TryAcquire(servidor, T0, TimeSpan.FromMinutes(30));
            db.CloudLeases.Add(lease);
            await db.SaveChangesAsync(Ct);
        }

        // Duas requisições leram o mesmo lease (mesma Version)...
        await using var primeira = factory.CreateDbContext();
        await using var segunda = factory.CreateDbContext();
        var a = await primeira.CloudLeases.SingleAsync(Ct);
        var b = await segunda.CloudLeases.SingleAsync(Ct);

        a.Advance(1);
        await primeira.SaveChangesAsync(Ct);

        // ...a segunda grava em cima de um estado que já mudou: o EF recusa.
        b.Advance(1);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => segunda.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Item_e_saldo_fazem_ida_e_volta_com_os_bytes_intactos()
    {
        using var factory = new SqliteTestFactory();
        var vault = await NovaNuvem(factory);
        var bytes = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
        var canal = new CloudChannel
        {
            VaultId = vault.Id, PlayerUuid = new string('b', 32), Name = CloudChannel.DefaultName
        };
        var item = new CloudItemType
        {
            Fingerprint = new string('f', 64),
            ItemId = "minecraft:diamond",
            ModId = "minecraft",
            DisplayName = "Diamante",
            Encoded = bytes
        };
        var saldo = new CloudBalance { ChannelId = canal.Id, ItemTypeId = item.Id };
        saldo.Apply(5_000_000_000L); // passa de int: a coluna tem de ser bigint

        await using (var db = factory.CreateDbContext())
        {
            db.AddRange(canal, item, saldo);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = factory.CreateDbContext())
        {
            (await db.CloudItemTypes.SingleAsync(Ct)).Encoded.ShouldBe(bytes);
            (await db.CloudBalances.SingleAsync(Ct)).Amount.ShouldBe(5_000_000_000L);
        }
    }

    [Fact]
    public void Payload_da_quarentena_nao_tem_limite_de_tamanho()
    {
        using var factory = new SqliteTestFactory();
        using var db = factory.CreateDbContext();

        db.Model.FindEntityType(typeof(CloudQuarantine))!
            .FindProperty(nameof(CloudQuarantine.PayloadJson))!
            .GetMaxLength()
            .ShouldBeNull("o lote cresce com o número de operações");
    }

    [Fact]
    public async Task Heartbeat_renova_so_o_que_e_do_servidor_e_nao_mexe_na_versao_no_sqlite()
    {
        using var factory = new SqliteTestFactory();
        await HeartbeatEmLote(factory);
    }

    [Fact]
    public async Task Heartbeat_renova_so_o_que_e_do_servidor_e_nao_mexe_na_versao_no_postgres()
    {
        // O "jogador está na lista" vira json_each no SQLite e array no
        // PostgreSQL: é na tradução que o UPDATE em lote quebraria.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null,
            "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).");
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await HeartbeatEmLote(postgres);
    }

    private static async Task HeartbeatEmLote(IDbContextFactory<TcMineDbContext> factory)
    {
        var meu = Guid.CreateVersion7();
        var outro = Guid.CreateVersion7();
        var vault = new CloudVault { Name = "Nuvem", OwnerId = Guid.CreateVersion7() };
        var ana = Lease(vault.Id, 'a', meu);
        var beto = Lease(vault.Id, 'b', meu);
        var tomado = Lease(vault.Id, 'c', outro);
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            db.CloudVaults.Add(vault);
            db.CloudLeases.AddRange(ana, beto, tomado);
            await db.SaveChangesAsync(Ct);
        }

        var repo = new CloudStorageRepository(factory);
        var lidos = await repo.GetLeasesAsync(vault.Id, [ana.PlayerUuid, tomado.PlayerUuid, new string('f', 32)], Ct);
        var agora = T0.AddMinutes(5);
        var renovados = await repo.RenewLeasesAsync(vault.Id, meu,
            [ana.PlayerUuid, tomado.PlayerUuid], agora, TimeSpan.FromMinutes(30), Ct);

        lidos.Select(l => l.PlayerUuid).ShouldBe([ana.PlayerUuid, tomado.PlayerUuid], ignoreOrder: true,
            "o jogador sem lease simplesmente não volta");
        renovados.ShouldBe(1, "o lease que outro servidor segura não casa com o filtro do dono");
        await using var depois = await factory.CreateDbContextAsync(Ct);
        var porJogador = await depois.CloudLeases.AsNoTracking().ToDictionaryAsync(l => l.PlayerUuid, Ct);
        porJogador[ana.PlayerUuid].ExpiresAt.ShouldBe(agora.AddMinutes(30));
        porJogador[ana.PlayerUuid].Version.ShouldBe(ana.Version,
            "renovar não muda dono, época nem sequência: um lote que leu antes ainda grava");
        porJogador[beto.PlayerUuid].ExpiresAt.ShouldBe(beto.ExpiresAt, "não estava no heartbeat");
        porJogador[tomado.PlayerUuid].ExpiresAt.ShouldBe(tomado.ExpiresAt);
    }

    private static CloudLease Lease(Guid vaultId, char jogador, Guid servidor)
    {
        var lease = new CloudLease { VaultId = vaultId, PlayerUuid = new string(jogador, 32) };
        lease.TryAcquire(servidor, T0, TimeSpan.FromMinutes(30));
        return lease;
    }

    private static async Task<CloudVault> NovaNuvem(SqliteTestFactory factory)
    {
        var vault = new CloudVault { Name = "Nuvem", OwnerId = Guid.CreateVersion7() };
        await using var db = factory.CreateDbContext();
        db.CloudVaults.Add(vault);
        await db.SaveChangesAsync(Ct);
        return vault;
    }

    private static CloudBatch Lote(Guid vaultId, long seq) => new()
    {
        VaultId = vaultId,
        PlayerUuid = new string('a', 32),
        ServerId = Guid.CreateVersion7(),
        Epoch = 1,
        Seq = seq,
        PayloadHash = new string('0', 64)
    };
}
