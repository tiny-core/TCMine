using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Uma verificação de atualizações por versão, de cada vez.
///     Desabilitar o botão na tela não resolvia: o admin fecha o diálogo, a
///     verificação continua em background, e o clique seguinte disparava outra.
///     Duas varreduras dos mesmos 483 mods, contra a mesma cota de API, e duas
///     barras de progresso idênticas empilhadas na tela.
///     Por isso a recusa mora no caso de uso, e não na página: qualquer ponto de
///     entrada fica coberto.
/// </summary>
public sealed class DuplicateJobGuardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recusa_uma_segunda_verificacao_da_mesma_versao()
    {
        var version = Versao();
        var progresso = new FakeJobProgress();
        progresso.EmCurso.Add(version.Id);

        var result = await new CheckModpackVersionUpdates(
                new FakeRepo(version), [], progresso)
            .HandleAsync(version.Id, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("em curso");
    }

    [Fact]
    public async Task Deixa_passar_quando_nada_esta_em_curso()
    {
        var version = Versao();

        var result = await new CheckModpackVersionUpdates(
                new FakeRepo(version), [], new FakeJobProgress())
            .HandleAsync(version.Id, Ct);

        // O conteúdo do resultado não importa aqui — sem origem configurada ele
        // não encontra nada. O que se afirma é que a guarda não barrou.
        result.Error.ShouldNotBe("Já há uma verificação em curso para esta versão. Espere terminar.");
    }

    private static ModpackVersion Versao()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "jei"
        });

        return version;
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(new Modpack
            {
                Name = "Pack", Slug = "pack", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
            });
    }
}
