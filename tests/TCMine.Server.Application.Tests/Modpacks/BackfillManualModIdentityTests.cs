using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Os mods enviados à mão antes da identidade existir ganham a sua no
///     arranque — inclusive em versões publicadas, porque a identidade não vai
///     no manifesto do jogador.
/// </summary>
public sealed class BackfillManualModIdentityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Le_o_id_do_jar_e_grava_so_a_identidade()
    {
        var file = Manual("mods/jei-1.2.jar");
        var repo = new Repo(file);

        var count = await new BackfillManualModIdentity(repo, new Blobs(exists: true), new FakeJarInspector(modId: "jei"))
            .HandleAsync(Ct);

        count.ShouldBe(1);
        repo.SlugsGravados[file.Id].ShouldBe("jar:jei");
    }

    [Fact]
    public async Task Blob_sumido_fica_com_a_identidade_pelo_nome()
    {
        var file = Manual("mods/Coisa.jar");
        var repo = new Repo(file);

        await new BackfillManualModIdentity(repo, new Blobs(exists: false), new FakeJarInspector(modId: "jei"))
            .HandleAsync(Ct);

        repo.SlugsGravados[file.Id].ShouldBe("file:coisa.jar");
    }

    private static ModpackFile Manual(string path) => new()
    {
        ModpackVersionId = Guid.CreateVersion7(),
        Path = path,
        Sha256 = new string('c', 64),
        SizeBytes = 1,
        Side = FileSide.Both,
        Origin = ModFileOrigin.ManualUpload
    };

    private sealed class Repo(params ModpackFile[] files) : FakeModpackRepositoryBase
    {
        public override Task<IReadOnlyList<ModpackFile>> ListManualModsWithoutIdentityAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ModpackFile>>(files);
    }

    private sealed class Blobs(bool exists) : FakeBlobStoreBase
    {
        public override Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(exists);

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[8]));
    }
}
