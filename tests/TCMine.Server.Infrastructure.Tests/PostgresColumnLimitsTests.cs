using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Grava, num PostgreSQL de verdade, os valores no tamanho máximo que o
///     domínio permite.
///     É o teste que faltava. A suíte inteira roda em SQLite, que aceita
///     qualquer texto numa coluna <c>varchar(n)</c> — então uma coluna curta
///     demais passava por tudo e só aparecia ao importar um pack real, com um
///     erro do banco que não nomeia a coluna. Aqui o limite é regra, como em
///     produção.
///     Sem TCMINE_TEST_POSTGRES, os testes são pulados: quem roda a suíte na
///     própria máquina não precisa de um banco de pé para ver o resto passar.
/// </summary>
public sealed class PostgresColumnLimitsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Override_com_caminho_no_limite_cabe_no_banco()
    {
        // O bug real: o slug de um override é o caminho MAIS um prefixo. Com o
        // caminho no tamanho máximo, é o slug que estoura — e era ele que não
        // tinha limite próprio.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await using var db = postgres.CreateContext();

        var caminho = "config/" + new string('x', ModpackFile.MaxPathLength - "config/".Length);
        var versao = await SemearVersaoAsync(db);

        db.ModpackFiles.Add(new ModpackFile
        {
            ModpackVersionId = versao,
            Path = caminho,
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Override,
            ProjectSlug = ModpackFile.OverrideSlug(caminho)
        });

        await Should.NotThrowAsync(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Icone_com_url_longa_cabe_no_banco()
    {
        // URL de CDN com assinatura passa de 512 com facilidade, e isto quebraria
        // na etapa seguinte à importação, ao baixar os mods.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await using var db = postgres.CreateContext();

        var versao = await SemearVersaoAsync(db);

        db.ModpackFiles.Add(new ModpackFile
        {
            ModpackVersionId = versao,
            Path = "mods/exemplo.jar",
            Sha256 = new string('b', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "exemplo",
            IconUrl = "https://cdn.exemplo.com/" + new string('u', 900)
        });

        await Should.NotThrowAsync(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task As_migrations_aplicam_num_banco_vazio()
    {
        // O caminho que toda instalação nova percorre, e que só existe de
        // verdade no provider de produção: o assembly de migrations do Postgres
        // nunca é exercido pela suíte em SQLite.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await using var db = postgres.CreateContext();

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        (await db.Database.CanConnectAsync(Ct)).ShouldBeTrue();
    }

    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private static async Task<Guid> SemearVersaoAsync(Persistence.TcMineDbContext db)
    {
        var modpack = new Modpack
        {
            Slug = $"teste-{Guid.CreateVersion7():N}"[..20],
            Name = "Teste",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        var versao = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = "1.0.0",
            LoaderVersion = "21.1.100"
        };

        db.Modpacks.Add(modpack);
        db.ModpackVersions.Add(versao);
        await db.SaveChangesAsync(Ct);

        return versao.Id;
    }
}
