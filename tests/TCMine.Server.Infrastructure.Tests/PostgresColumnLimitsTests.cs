using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Application.Modpacks;

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
    public async Task Snapshot_de_pack_grande_cabe_no_banco()
    {
        // A reprodução do bug relatado: importar um pack do CurseForge morria
        // com "value too long for type character varying(512)" mesmo depois de
        // alargar as colunas do arquivo. Faltava esta: o snapshot da origem
        // guarda um par projeto/arquivo e o nome de CADA mod, então um pack de
        // trezentos mods gera dezenas de KB numa coluna que a configuração
        // dizia não ter limite — e tinha.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await using var db = postgres.CreateContext();

        var mods = Enumerable.Range(0, 300).ToDictionary(
            i => $"projeto-{i:D6}",
            i => $"arquivo-{i:D8}");

        var snapshot = new UpstreamSnapshot
        {
            Mods = mods,
            Names = mods.ToDictionary(m => m.Key, m => $"Mod de Exemplo com Nome Longo {m.Key}"),
            Overrides = Enumerable.Range(0, 200).ToDictionary(
                i => $"config/exemplo/arquivo-{i:D4}.json",
                _ => new string('c', 64))
        }.ToJson();

        // Se isto couber em 512 o teste não está exercendo nada.
        snapshot.Length.ShouldBeGreaterThan(20_000);

        var modpack = new Modpack
        {
            Slug = $"grande-{Guid.CreateVersion7():N}"[..20],
            Name = "Pack grande",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        db.Modpacks.Add(modpack);
        db.ModpackVersions.Add(new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = "1.0.0",
            LoaderVersion = "21.1.100",
            UpstreamSnapshotJson = snapshot
        });

        await Should.NotThrowAsync(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task A_consulta_de_recursos_traduz_no_postgres()
    {
        // A aba Recursos ficava carregando para sempre: a consulta usava
        // Any() sobre uma coleção local, que cada provider expande de um jeito.
        // Passava no SQLite da suíte e falhava no PostgreSQL de produção — a
        // mesma família de bug que o limite de coluna, e o motivo de estes
        // testes existirem.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        var repo = new ModpackRepository(new FabricaFixa(postgres));

        var modpack = new Modpack
        {
            Slug = $"pack-{Guid.CreateVersion7():N}"[..20],
            Name = "Pack",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        var versao = new ModpackVersion
        {
            ModpackId = modpack.Id, Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        versao.UpsertFile(ArquivoEm(versao.Id, "mods/jei.jar", "jei"));
        versao.UpsertFile(ArquivoEm(versao.Id, "shaderpacks/complementary.zip", "shader"));

        await repo.CreateAsync(modpack, Ct);
        await repo.AddVersionAsync(versao, Ct);

        var recursos = await repo.ListVersionFilesAsync(
            versao.Id, VersionFileScope.Assets, null, new PageRequest(0, 25), Ct);

        var mods = await repo.ListVersionFilesAsync(
            versao.Id, VersionFileScope.Mods, null, new PageRequest(0, 25), Ct);

        recursos.Items.Select(f => f.Path).ShouldBe(["shaderpacks/complementary.zip"]);
        mods.Items.Select(f => f.Path).ShouldBe(["mods/jei.jar"]);
    }

    private static ModpackFile ArquivoEm(Guid versionId, string path, string slug) => new()
    {
        ModpackVersionId = versionId,
        Path = path,
        Sha256 = new string('a', 64),
        SizeBytes = 10,
        Side = FileSide.Both,
        Origin = ModFileOrigin.CurseForge,
        ProjectSlug = slug
    };

    /// <summary>O repositório pede uma fábrica; aqui todas as chamadas vão ao mesmo banco.</summary>
    private sealed class FabricaFixa(PostgresTestDatabase db) : IDbContextFactory<TcMineDbContext>
    {
        public TcMineDbContext CreateDbContext() => db.CreateContext();
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
