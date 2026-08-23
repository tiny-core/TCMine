using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Trocar o lado de um arquivo à mão.
///     É a saída para o que nenhuma origem responde: o manifest do CurseForge
///     não declara lado, as tags Client/Server da API faltam na maioria dos
///     arquivos, e o <c>neoforge.mods.toml</c> não tem campo de lado por mod —
///     o Colorwheel, que só serve para usar shaders no cliente, declara todas as
///     dependências como BOTH. Sem isto, um mod de cliente vai para o container
///     do servidor e o jogo não sobe.
/// </summary>
public sealed class ChangeFileSideTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grava_o_lado_novo()
    {
        var version = Rascunho();
        var arquivo = version.Files.Single();
        var repo = new FakeRepo(version);

        var result = await new ChangeFileSide(repo)
            .HandleAsync(version.Id, arquivo.Id, FileSide.ClientOnly, Ct);

        result.Succeeded.ShouldBeTrue(result.Error);
        repo.LadosGravados[arquivo.Id].ShouldBe(FileSide.ClientOnly);
    }

    [Fact]
    public async Task Nao_escreve_quando_o_lado_ja_e_esse()
    {
        // Clicar no valor que já está lá não é motivo para tocar o banco.
        var version = Rascunho();
        var arquivo = version.Files.Single();
        var repo = new FakeRepo(version);

        var result = await new ChangeFileSide(repo)
            .HandleAsync(version.Id, arquivo.Id, FileSide.Both, Ct);

        result.Succeeded.ShouldBeTrue();
        repo.LadosGravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Recusa_versao_publicada()
    {
        var version = Rascunho();
        var arquivo = version.Files.Single();
        version.MarkResolving();
        version.MarkReady();

        var repo = new FakeRepo(version);

        var result = await new ChangeFileSide(repo)
            .HandleAsync(version.Id, arquivo.Id, FileSide.ClientOnly, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("rascunho");
        repo.LadosGravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Recusa_arquivo_de_outra_versao()
    {
        var version = Rascunho();
        var repo = new FakeRepo(version);

        var result = await new ChangeFileSide(repo)
            .HandleAsync(version.Id, Guid.CreateVersion7(), FileSide.ClientOnly, Ct);

        result.Succeeded.ShouldBeFalse();
        repo.LadosGravados.ShouldBeEmpty();
    }

    private static ModpackVersion Rascunho()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/colorwheel.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "colorwheel"
        });

        return version;
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);
    }
}
