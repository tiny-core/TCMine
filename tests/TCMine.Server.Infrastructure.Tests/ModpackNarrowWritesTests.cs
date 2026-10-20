using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Leitura leve e gravações estreitas do modpack (6.2). Regressão: renomear
///     fazia Update() no modpack carregado COM as versões e as pendências, e o EF
///     regravava o grafo inteiro com a cópia lida antes — uma versão que a
///     ingestão tinha acabado de pôr em Resolvendo voltava a Rascunho, e uma
///     pendência já resolvida (apagada) derrubava a gravação.
/// </summary>
public sealed class ModpackNarrowWritesTests : IDisposable
{
    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private readonly SqliteTestFactory _factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public Task Renomear_nao_regrava_as_versoes_lidas_antes() => RenomearPreservaVersoes(_factory);

    [Fact]
    public async Task Renomear_nao_regrava_as_versoes_lidas_antes_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await RenomearPreservaVersoes(postgres);
    }

    [Fact]
    public Task Nome_e_capa_nao_se_desfazem_um_ao_outro() => NomeECapa(_factory);

    [Fact]
    public async Task Nome_e_capa_nao_se_desfazem_um_ao_outro_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await NomeECapa(postgres);
    }

    [Fact]
    public async Task Cabecalho_vem_sem_as_versoes()
    {
        var repo = new ModpackRepository(_factory, new RecordingResourceChanges());
        var (modpack, _) = await Semear(repo);

        var header = await repo.GetHeaderAsync(modpack.Id, Ct);

        header.ShouldNotBeNull();
        header.Name.ShouldBe("Original");
        header.MinecraftVersion.ShouldBe("1.21.1");
        header.Versions.ShouldBeEmpty("o cabeçalho não carrega versões, mesmo que existam");
        (await repo.GetByIdAsync(modpack.Id, Ct))!.Versions.ShouldHaveSingleItem();
        (await repo.GetHeaderAsync(Guid.CreateVersion7(), Ct)).ShouldBeNull();
    }

    private static async Task RenomearPreservaVersoes(IDbContextFactory<TcMineDbContext> factory)
    {
        var repo = new ModpackRepository(factory, new RecordingResourceChanges());
        var (modpack, version) = await Semear(repo);

        // A tela de detalhe tem o modpack com as versões, lido antes.
        var tela = await repo.GetByIdAsync(modpack.Id, Ct);
        tela!.Versions.Single().PendingMods.ShouldHaveSingleItem();

        // A ingestão começa: a versão vai para Resolvendo e a pendência resolve.
        var emIngestao = (await repo.GetVersionAsync(version.Id, Ct))!;
        emIngestao.MarkResolving();
        await repo.SaveVersionStateAsync(emIngestao, Ct);
        await repo.RemovePendingAsync(version.Id, emIngestao.PendingMods.Single().Id, Ct);

        // O admin renomeia a partir da tela.
        await repo.UpdateDetailsAsync(modpack.Id, "Nome novo", "resumo", Ct);

        var lida = (await repo.GetVersionAsync(version.Id, Ct))!;
        lida.State.ShouldBe(ModpackVersionState.Resolving, "renomear não pode devolver a versão a rascunho");
        lida.PendingMods.ShouldBeEmpty("a pendência resolvida não volta");
        var header = (await repo.GetHeaderAsync(modpack.Id, Ct))!;
        header.Name.ShouldBe("Nome novo");
        header.Summary.ShouldBe("resumo");
        header.UpdatedAt.ShouldNotBeNull();
    }

    private static async Task NomeECapa(IDbContextFactory<TcMineDbContext> factory)
    {
        var repo = new ModpackRepository(factory, new RecordingResourceChanges());
        var (modpack, _) = await Semear(repo);
        var capa = new string('c', 64);

        await repo.SetIconAsync(modpack.Id, capa, Ct);
        await repo.UpdateDetailsAsync(modpack.Id, "Nome novo", null, Ct);

        var header = (await repo.GetHeaderAsync(modpack.Id, Ct))!;
        header.IconBlobSha256.ShouldBe(capa, "salvar o nome não apaga a capa trocada antes");
        header.Name.ShouldBe("Nome novo");
        header.Summary.ShouldBeNull();
        header.Slug.ShouldBe("original", "o que é imutável continua igual");
    }

    private static async Task<(Modpack, ModpackVersion)> Semear(ModpackRepository repo)
    {
        var modpack = new Modpack
        {
            Slug = "original", Name = "Original", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
        };
        await repo.CreateAsync(modpack, Ct);

        var version = new ModpackVersion { ModpackId = modpack.Id, Version = "1.0.0", LoaderVersion = "21.1.1" };
        version.UpsertPending(new PendingMod
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "jei",
            DisplayName = "JEI",
            Origin = ModFileOrigin.Modrinth,
            Reason = PendingModReason.Queued
        });
        await repo.AddVersionAsync(version, Ct);
        return (modpack, version);
    }
}
