using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A BackfillCloudWatermarks recria, em SQL, a régua que o hello usava antes
///     dela: o maior lote APLICADO de cada (nuvem, servidor, jogador). O banco
///     para na AddCloudWatermarks, ganha lotes "antigos" e só então sobe o resto
///     — o mesmo caminho da produção. Num PostgreSQL de verdade (pulado sem
///     TCMINE_TEST_POSTGRES; o CI define).
/// </summary>
public sealed class CloudWatermarkMigrationTests
{
    private const string Ana = "069a79f444e94726a5befca90e38aaf5";
    private const string Beto = "11111111222233334444555566667777";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preenchimento_pega_o_maior_lote_aplicado_de_cada_servidor_e_jogador()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null,
            "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).");
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct, "20261017152407_AddCloudWatermarks");

        var dono = Guid.CreateVersion7();
        var vault = new CloudVault { Name = "N", OwnerId = dono };
        var a = Servidor("A", 25565, dono);
        var b = Servidor("B", 25566, dono);
        var (nuvem, servidorA, servidorB) = (vault.Id, a.Id, b.Id);

        // Pelas colunas que o banco tem NESTA migration, e não pelo modelo de
        // hoje: coluna nova em cloud_vaults ou game_servers não pode quebrar o
        // teste de uma migration antiga.
        await postgres.InsertAsOfSchemaAsync(
        [
            vault, a, b,
            // Ana no A: a época 2 vence a sequência alta da época 1; o estornado
            // e o em quarentena, mesmo maiores, não contam.
            Lote(nuvem, servidorA, Ana, 1, 9),
            Lote(nuvem, servidorA, Ana, 2, 3),
            Lote(nuvem, servidorA, Ana, 2, 4, l => l.MarkReverted()),
            Lote(nuvem, servidorA, Ana, 3, 1, l => l.MarkQuarantined()),
            // Ana no B tem a marca dela, separada da do A.
            Lote(nuvem, servidorB, Ana, 5, 1),
            Lote(nuvem, servidorA, Beto, 1, 2),
            // Servidor apagado: lote sem servidor não vira marca.
            Lote(nuvem, Guid.CreateVersion7(), Beto, 7, 7)
        ], Ct);

        await postgres.MigrateAsync(null, Ct);

        await using var depois = postgres.CreateContext();
        var marcas = await depois.CloudWatermarks.AsNoTracking()
            .ToDictionaryAsync(w => (w.ServerId, w.PlayerUuid), w => (w.Epoch, w.Seq), Ct);
        marcas.Count.ShouldBe(3);
        marcas[(servidorA, Ana)].ShouldBe((2L, 3L));
        marcas[(servidorB, Ana)].ShouldBe((5L, 1L));
        marcas[(servidorA, Beto)].ShouldBe((1L, 2L));
        (await depois.CloudWatermarks.AsNoTracking().AllAsync(w => w.VaultId == nuvem, Ct)).ShouldBeTrue();
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

    private static CloudBatch Lote(Guid nuvem, Guid servidor, string jogador, long epoch, long seq,
        Action<CloudBatch>? estado = null)
    {
        var lote = new CloudBatch
        {
            VaultId = nuvem,
            ServerId = servidor,
            PlayerUuid = jogador,
            Epoch = epoch,
            Seq = seq,
            PayloadHash = new string('a', 64)
        };
        estado?.Invoke(lote);
        return lote;
    }
}
