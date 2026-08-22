using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Upload manual de arquivo para uma versão em rascunho.
///     A garantia que pesa aqui é o caminho: ele vem digitado pelo admin e vira
///     posição de arquivo dentro da pasta da instância. Um ".." aceito
///     escreveria fora dela — e a pasta da instância é montada como volume no
///     container do jogo.
/// </summary>
public sealed class AddManualFileTests
{
    [Theory]
    [InlineData("../fora.jar")]
    [InlineData("mods/../../fora.jar")]
    [InlineData("..\\fora.jar")]
    [InlineData("mods/..")]
    public async Task Recusa_caminho_que_escapa_da_instancia(string caminho)
    {
        var (caso, repo, blobs) = Montar();

        var result = await caso.HandleAsync(Comando(repo.Rascunho.Id, caminho), Ct);

        result.Succeeded.ShouldBeFalse();

        // Nem grava o blob: recusar depois de escrever deixaria lixo no store a
        // cada tentativa.
        blobs.Gravados.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public async Task Recusa_caminho_vazio(string caminho)
    {
        var (caso, repo, _) = Montar();

        var result = await caso.HandleAsync(Comando(repo.Rascunho.Id, caminho), Ct);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Normaliza_barra_invertida_e_barra_inicial()
    {
        // O admin digita o caminho como está acostumado no sistema dele; o pack
        // usa barra normal e caminho relativo, sempre.
        var (caso, repo, _) = Montar();

        var result = await caso.HandleAsync(Comando(repo.Rascunho.Id, "/config\\mod\\arquivo.toml"), Ct);

        result.Succeeded.ShouldBeTrue();
        result.Value!.Path.ShouldBe("config/mod/arquivo.toml");
    }

    [Fact]
    public async Task Recusa_arquivo_em_versao_publicada()
    {
        // Versão Ready é imutável: quem já instalou não receberia o arquivo
        // novo, e o manifesto deixaria de descrever o que está no disco.
        var (caso, repo, blobs) = Montar();

        var result = await caso.HandleAsync(Comando(repo.Publicada.Id, "mods/x.jar"), Ct);

        result.Succeeded.ShouldBeFalse();
        blobs.Gravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Recusa_caminho_repetido_ignorando_a_caixa()
    {
        // Dois arquivos no mesmo lugar tornariam indeterminado qual vale, e no
        // disco um sobrescreveria o outro.
        var (caso, repo, _) = Montar();

        await caso.HandleAsync(Comando(repo.Rascunho.Id, "mods/JEI.jar"), Ct);
        var segunda = await caso.HandleAsync(Comando(repo.Rascunho.Id, "mods/jei.jar"), Ct);

        segunda.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Versao_inexistente_e_recusada()
    {
        var (caso, _, _) = Montar();

        var result = await caso.HandleAsync(Comando(Guid.CreateVersion7(), "mods/x.jar"), Ct);

        result.Succeeded.ShouldBeFalse();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AddManualFileCommand Comando(Guid versionId, string path) =>
        new(versionId, path, new MemoryStream(Encoding.UTF8.GetBytes("conteudo")),
            "application/java-archive", FileSide.Both, false);

    private static (AddManualFile Caso, FakeRepo Repo, FakeBlobs Blobs) Montar()
    {
        var repo = new FakeRepo();
        var blobs = new FakeBlobs();
        return (new AddManualFile(repo, blobs), repo, blobs);
    }

    private sealed class FakeRepo : FakeModpackRepositoryBase
    {
        public ModpackVersion Rascunho { get; } = new()
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        public ModpackVersion Publicada { get; } = Pronta();

        public override Task<ModpackVersion?> GetVersionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(
                id == Rascunho.Id ? Rascunho : id == Publicada.Id ? Publicada : null);

        public override Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct) =>
            Task.CompletedTask;

        private static ModpackVersion Pronta()
        {
            var version = new ModpackVersion
            {
                ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
            };

            version.UpsertFile(new ModpackFile
            {
                ModpackVersionId = version.Id,
                Path = "mods/base.jar",
                Sha256 = new string('a', 64),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.Modrinth,
                ProjectSlug = "base"
            });

            version.MarkResolving();
            version.MarkReady();
            return version;
        }
    }

    private sealed class FakeBlobs : FakeBlobStoreBase
    {
        public List<string> Gravados { get; } = [];

        public override async Task<string> PutAsync(
            Stream content, string? expectedSha256, string? contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);

            var sha = new string('b', 64);
            Gravados.Add(sha);
            return sha;
        }

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[8]));
    }
}
