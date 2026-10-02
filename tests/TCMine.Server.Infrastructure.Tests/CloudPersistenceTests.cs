using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Cloud;

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
            db.CloudBatches.Add(Lote(vault.Id, seq: 1));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = factory.CreateDbContext())
        {
            db.CloudBatches.Add(Lote(vault.Id, seq: 1));
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
        var canal = new CloudChannel { VaultId = vault.Id, PlayerUuid = new string('b', 32), Name = CloudChannel.DefaultName };
        var item = new CloudItemType
        {
            Fingerprint = new string('f', 64), ItemId = "minecraft:diamond", ModId = "minecraft",
            DisplayName = "Diamante", Encoded = bytes
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
        VaultId = vaultId, PlayerUuid = new string('a', 32), ServerId = Guid.CreateVersion7(),
        Epoch = 1, Seq = seq, PayloadHash = new string('0', 64)
    };
}
