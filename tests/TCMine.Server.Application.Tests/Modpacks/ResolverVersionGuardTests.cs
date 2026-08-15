using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Um mod da versão errada do Minecraft não falha na ingestão: ele instala
///     e derruba o servidor no arranque, com uma exceção que não aponta para o
///     TCMine. Estes testes travam o comportamento no ponto em que ainda dá para
///     perceber — antes de gravar.
/// </summary>
public sealed class ResolverVersionGuardTests
{
    [Fact]
    public async Task Mod_resolvido_para_outra_versao_do_minecraft_vira_pendencia()
    {
        var version = NovaVersao();
        var repo = new FakeRepo(version);

        // O resolver devolveu NotFound porque conferiu e o arquivo era de outra
        // versão do MC. A ingestão registra como pendência, não como sucesso.
        var resolver = new RecusaResolver("declara [26.2], e não Minecraft 1.21.1");

        await new ModpackIngestionService(
                repo, new FakeBlob(), [resolver], new FakeDownloader(),
                new FakeJarInspector(), new FakeJobProgress(), NullLogger<ModpackIngestionService>.Instance)
            .IngestAsync(version.Id, [Item("jei")], CancellationToken.None);

        Assert.Empty(version.Files);

        var pendente = Assert.Single(version.PendingMods);
        Assert.Equal(PendingModReason.NoCompatibleFile, pendente.Reason);
        Assert.Contains("26.2", pendente.Detail);
    }

    // ---- Fixtures ----

    private static ModpackVersion NovaVersao() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100" };

    private static ModIngestionItem Item(string slug) =>
        new(ModFileOrigin.Modrinth, slug, null, FileSide.Both);

    // ---- Fakes ----

    private sealed class RecusaResolver(string motivo) : IModResolver
    {
        public ModFileOrigin Origin => ModFileOrigin.Modrinth;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct) =>
            Task.FromResult<ModResolution>(new ModResolution.NotFound(motivo));
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

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task SaveVersionStateAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task AddFilesAsync(
            Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeBlob : FakeBlobStoreBase;

    private sealed class FakeDownloader : IModDownloader
    {
        public Task<Stream> OpenAsync(Uri url, CancellationToken ct) =>
            throw new InvalidOperationException("não deveria baixar nada");
    }
}
