using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Caminho de arquivo vem do admin (formulário de upload, editor de
///     overrides) e vira caminho REAL dentro da pasta da instância. Um ".." que
///     passe daqui escreve fora dela — no host que roda o painel.
/// </summary>
public sealed class FilePathGuardTests
{
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("mods/../../../fora.jar")]
    [InlineData("..\\..\\windows\\system32\\x.dll")]
    public async Task Upload_recusa_caminho_que_escapa_da_instancia(string caminho)
    {
        var version = Rascunho();

        var result = await NewUpload(version).HandleAsync(
            Comando(version.Id, caminho), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(version.Files);
    }

    [Fact]
    public async Task Upload_normaliza_barra_invertida_e_barra_inicial()
    {
        var version = Rascunho();

        var result = await NewUpload(version).HandleAsync(
            Comando(version.Id, "/mods\\jei.jar"), CancellationToken.None);

        Assert.True(result.Succeeded);

        // Windows manda barra invertida; a instância usa barra normal e caminho
        // relativo. Sem normalizar, o mesmo arquivo entraria duas vezes.
        Assert.Equal("mods/jei.jar", version.Files[0].Path);
    }

    [Fact]
    public async Task Upload_recusa_caminho_ja_ocupado_na_versao()
    {
        var version = Rascunho();
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei.jar",
            Sha256 = new string('b', 64),
            SizeBytes = 1,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Modrinth,
            ProjectSlug = "jei"
        });

        // Dois arquivos no mesmo caminho não podem coexistir na pasta.
        var result = await NewUpload(version).HandleAsync(
            Comando(version.Id, "mods/jei.jar"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Upload_so_e_permitido_em_rascunho()
    {
        // Versão publicada é imutável: é a promessa que faz um pack que
        // funcionava continuar funcionando daqui a um ano.
        var version = Rascunho();
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/x.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 1,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Modrinth,
            ProjectSlug = "x"
        });
        version.MarkResolving();
        version.MarkReady();

        var result = await NewUpload(version).HandleAsync(
            Comando(version.Id, "mods/novo.jar"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    // ---- Fixtures ----

    private static ModpackVersion Rascunho() => new()
    {
        ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
    };

    private static AddManualFile NewUpload(ModpackVersion version) =>
        new(new FakeRepo(version), new FakeBlobStore());

    private static AddManualFileCommand Comando(Guid versionId, string path) =>
        new(versionId, path, new MemoryStream(Encoding.UTF8.GetBytes("conteudo")),
            "application/java-archive", FileSide.Both, false);

    // ---- Fakes ----

    private sealed class FakeBlobStore : FakeBlobStoreBase
    {
        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('c', 64));

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[8]));
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) =>
            Task.CompletedTask;

        public override Task RemovePendingAsync(Guid versionId, Guid pendingId, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
