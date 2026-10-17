using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Auditoria da nuvem (5.2) contra um banco de verdade: cursor, filtros e
///     isolamento entre nuvens. O cursor e o período comparam o Id (GUID) no
///     SQL — é na tradução que isso quebraria, e o SQLite (GUID como texto) e o
///     PostgreSQL (uuid) comparam de jeitos diferentes, por isso os dois.
/// </summary>
public sealed class CloudAuditQueryTests
{
    private const string Ana = "069a79f444e94726a5befca90e38aaf5";
    private const string Beto = "11111111222233334444555566667777";

    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Paginas_do_ledger_cobrem_tudo_do_mais_novo_ao_mais_velho_sem_repetir_no_sqlite()
    {
        using var factory = new SqliteTestFactory();
        await PaginasCobremTudo(factory);
    }

    [Fact]
    public async Task Paginas_do_ledger_cobrem_tudo_do_mais_novo_ao_mais_velho_sem_repetir_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await PaginasCobremTudo(postgres);
    }

    [Fact]
    public async Task Filtros_de_jogador_item_e_origem_no_sqlite()
    {
        using var factory = new SqliteTestFactory();
        await FiltrosFuncionam(factory);
    }

    [Fact]
    public async Task Filtros_de_jogador_item_e_origem_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await FiltrosFuncionam(postgres);
    }

    [Fact]
    public async Task Periodo_pega_so_o_que_foi_gravado_entre_as_duas_datas()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await SemearNuvens(factory);

        // Três levas separadas por alguns ms: o período [inicio, fim) tem de
        // trazer só a do meio. É o Id (GUID v7) que carrega o instante.
        var antes = await GravarAsync(factory, cenario, cenario.CanalAna, 1);
        await Task.Delay(15, Ct);
        var inicio = DateTimeOffset.UtcNow;
        await Task.Delay(15, Ct);
        var meio = await GravarAsync(factory, cenario, cenario.CanalAna, 2);
        await Task.Delay(15, Ct);
        var fim = DateTimeOffset.UtcNow;
        await Task.Delay(15, Ct);
        var depois = await GravarAsync(factory, cenario, cenario.CanalAna, 1);

        var pagina = await new CloudGovernanceRepository(factory).ListLedgerAsync(
            Consulta(cenario.Nuvem) with { From = inicio, Until = fim }, Ct);

        pagina.Items.Select(l => l.Id).ShouldBe(meio, ignoreOrder: true);
        pagina.Items.ShouldNotContain(l => antes.Contains(l.Id) || depois.Contains(l.Id));
    }

    [Fact]
    public async Task Decisoes_do_painel_tambem_vem_por_cursor_e_so_da_propria_nuvem()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await SemearNuvens(factory);
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            for (var i = 0; i < 5; i++)
                db.CloudAdminAudit.Add(Decisao(cenario.Nuvem, $"acao.{i}"));
            db.CloudAdminAudit.Add(Decisao(cenario.OutraNuvem, "alheia"));
            await db.SaveChangesAsync(Ct);
        }

        var repo = new CloudGovernanceRepository(factory);
        var primeira = await repo.ListAuditAsync(cenario.Nuvem, null, 3, Ct);
        var segunda = await repo.ListAuditAsync(cenario.Nuvem, primeira.Next, 3, Ct);

        primeira.Items.Count.ShouldBe(3);
        primeira.Next.ShouldNotBeNull();
        segunda.Items.Count.ShouldBe(2);
        segunda.Next.ShouldBeNull();
        primeira.Items.Concat(segunda.Items).Select(a => a.Action).Distinct().Count().ShouldBe(5);
        primeira.Items.Concat(segunda.Items).ShouldNotContain(a => a.Action == "alheia");
    }

    [Fact]
    public async Task Migration_de_preenchimento_da_a_nuvem_do_canal_as_linhas_antigas()
    {
        // As linhas gravadas antes da coluna nascem com o GUID vazio, e sem a
        // BackfillLedgerVaultId elas somem da auditoria da nuvem inteira. O banco
        // para na AddLedgerVaultId, ganha linhas "antigas", e só então sobe o
        // resto — o mesmo caminho da produção.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct, "20261017141452_AddLedgerVaultId");
        var cenario = await SemearNuvens(postgres);
        var antigas = await GravarAsync(postgres, cenario, (Guid.Empty, cenario.CanalAna.Channel), 2);
        var daOutra = await GravarAsync(postgres, cenario, (Guid.Empty, cenario.CanalDeOutraNuvem.Channel), 1);

        await postgres.MigrateAsync(null, Ct);

        await using var db = postgres.CreateContext();
        var linhas = await db.CloudLedger.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.VaultId, Ct);
        antigas.ShouldAllBe(id => linhas[id] == cenario.Nuvem);
        daOutra.ShouldAllBe(id => linhas[id] == cenario.OutraNuvem, "a nuvem vem do canal de cada linha");
    }

    private static async Task PaginasCobremTudo(IDbContextFactory<TcMineDbContext> factory)
    {
        var cenario = await SemearNuvens(factory);
        var daNuvem = (await GravarAsync(factory, cenario, cenario.CanalAna, 4))
            .Concat(await GravarAsync(factory, cenario, cenario.CanalBeto, 3)).ToList();
        await GravarAsync(factory, cenario, cenario.CanalDeOutraNuvem, 2);

        var repo = new CloudGovernanceRepository(factory);
        var vistos = new List<Guid>();
        Guid? cursor = null;
        var paginas = 0;
        do
        {
            var pagina = await repo.ListLedgerAsync(Consulta(cenario.Nuvem) with { Before = cursor, PageSize = 3 },
                Ct);
            vistos.AddRange(pagina.Items.Select(l => l.Id));
            cursor = pagina.Next;
            paginas++;
        } while (cursor is not null && paginas < 10);

        paginas.ShouldBe(3, "7 linhas em páginas de 3");
        vistos.ShouldBe(daNuvem.OrderByDescending(BancoOrdena, StringComparer.Ordinal), ignoreOrder: false,
            "do mais novo ao mais velho, sem repetir, sem pular e sem linha de outra nuvem");
    }

    private static async Task FiltrosFuncionam(IDbContextFactory<TcMineDbContext> factory)
    {
        var cenario = await SemearNuvens(factory);
        var daAna = await GravarAsync(factory, cenario, cenario.CanalAna, 2);
        var doBeto = await GravarAsync(factory, cenario, cenario.CanalBeto, 1);
        var pedra = await GravarAsync(factory, cenario, cenario.CanalBeto, 1, cenario.Pedra);
        var estorno = await GravarAsync(factory, cenario, cenario.CanalAna, 1, origem: CloudLedgerSource.Revert);
        var repo = new CloudGovernanceRepository(factory);

        var ana = await repo.ListLedgerAsync(Consulta(cenario.Nuvem) with { PlayerUuid = Ana }, Ct);
        ana.Items.Select(l => l.Id).ShouldBe(daAna.Concat(estorno), ignoreOrder: true);
        ana.Items.ShouldAllBe(l => l.PlayerName == "Ana", "o nome vem da conta do TCMine");

        // Maiúsculas diferentes de propósito: no PostgreSQL o LIKE diferencia.
        var diamante = await repo.ListLedgerAsync(Consulta(cenario.Nuvem) with { Item = "DIAMANTE" }, Ct);
        diamante.Items.Select(l => l.Id).ShouldBe(daAna.Concat(doBeto).Concat(estorno), ignoreOrder: true);
        var porId = await repo.ListLedgerAsync(Consulta(cenario.Nuvem) with { Item = "cobble" }, Ct);
        porId.Items.Select(l => l.Id).ShouldBe(pedra);

        var estornos = await repo.ListLedgerAsync(Consulta(cenario.Nuvem) with { Source = CloudLedgerSource.Revert },
            Ct);
        estornos.Items.Select(l => l.Id).ShouldBe(estorno);

        var betoSemNome = await repo.ListLedgerAsync(Consulta(cenario.Nuvem) with { PlayerUuid = Beto }, Ct);
        betoSemNome.Items.ShouldAllBe(l => l.PlayerName == null);
    }

    /// <summary>A ordem em que o banco compara GUIDs: bytes big-endian (o texto do SQLite segue a mesma).</summary>
    private static string BancoOrdena(Guid id) => Convert.ToHexString(id.ToByteArray(bigEndian: true));

    private static CloudLedgerQuery Consulta(Guid nuvem) => new(nuvem, null, null, null, null, null, null, 50);

    private static CloudAdminAuditEntry Decisao(Guid nuvem, string acao) => new()
    {
        VaultId = nuvem,
        ActorUserId = Guid.CreateVersion7(),
        Action = acao,
        Details = "teste"
    };

    private static async Task<IReadOnlyList<Guid>> GravarAsync(IDbContextFactory<TcMineDbContext> factory,
        Cenario cenario, (Guid Vault, Guid Channel) canal, int quantas, Guid? item = null,
        CloudLedgerSource origem = CloudLedgerSource.Game)
    {
        await using var db = await factory.CreateDbContextAsync(Ct);
        var linhas = Enumerable.Range(1, quantas).Select(i => new CloudLedgerEntry
        {
            VaultId = canal.Vault,
            ChannelId = canal.Channel,
            ItemTypeId = item ?? cenario.Diamante,
            Delta = i,
            BalanceAfter = i,
            Source = origem
        }).ToList();
        db.CloudLedger.AddRange(linhas);
        await db.SaveChangesAsync(Ct);
        return [.. linhas.Select(l => l.Id)];
    }

    private static async Task<Cenario> SemearNuvens(IDbContextFactory<TcMineDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync(Ct);
        var dono = Guid.CreateVersion7();
        var nuvem = new CloudVault { Name = "N", OwnerId = dono };
        var outra = new CloudVault { Name = "Outra", OwnerId = dono };
        var ana = new CloudChannel { VaultId = nuvem.Id, PlayerUuid = Ana, Name = "Principal" };
        var beto = new CloudChannel { VaultId = nuvem.Id, PlayerUuid = Beto, Name = "Principal" };
        var anaNaOutra = new CloudChannel { VaultId = outra.Id, PlayerUuid = Ana, Name = "Principal" };
        var diamante = Item("minecraft:diamond", "Diamante", 'd');
        var pedra = Item("minecraft:cobblestone", "Pedregulho", 'c');
        db.AddRange(nuvem, outra, new User { DisplayName = "Ana", MinecraftUuid = Ana });
        db.AddRange(ana, beto, anaNaOutra, diamante, pedra);
        await db.SaveChangesAsync(Ct);
        return new Cenario(nuvem.Id, outra.Id, (nuvem.Id, ana.Id), (nuvem.Id, beto.Id), (outra.Id, anaNaOutra.Id),
            diamante.Id, pedra.Id);
    }

    private static CloudItemType Item(string id, string nome, char c) => new()
    {
        Fingerprint = new string(c, 64),
        ItemId = id,
        ModId = "minecraft",
        DisplayName = nome,
        Encoded = [1]
    };

    private sealed record Cenario(
        Guid Nuvem,
        Guid OutraNuvem,
        (Guid Vault, Guid Channel) CanalAna,
        (Guid Vault, Guid Channel) CanalBeto,
        (Guid Vault, Guid Channel) CanalDeOutraNuvem,
        Guid Diamante,
        Guid Pedra);
}
