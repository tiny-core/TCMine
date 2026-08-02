using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

public sealed class ModpackRepositoryTests : IDisposable
{
    private readonly SqliteTestFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task UpdateVersionAsync_persiste_mudanca_de_path_de_arquivo_existente()
    {
        // Regressão: o método marcava arquivos existentes como Unchanged, e a
        // edição in-place (mover override, renomear) era silenciosamente
        // ignorada. Este teste falha se alguém voltar a pôr Unchanged.
        var repo = new ModpackRepository(_factory);

        // Um modpack + uma versão com um override, gravados de verdade.
        var modpack = new Modpack
        {
            Slug = "teste", Name = "Teste", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
        };
        await repo.CreateAsync(modpack, CancellationToken.None);

        var version = new ModpackVersion { ModpackId = modpack.Id, Version = "1.0", LoaderVersion = "21.1.234" };
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "config/a.toml",
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Override,
            ProjectSlug = "override:config/a.toml"
        });
        await repo.AddVersionAsync(version, CancellationToken.None);

        // Recarrega, edita o path IN-PLACE e regrava (o que o move faz).
        var toEdit = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        toEdit!.Files.Single().Path = "backup/a.toml";
        await repo.UpdateVersionAsync(toEdit, CancellationToken.None);

        // Relê de um contexto novo: a mudança tem de estar no banco.
        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Equal("backup/a.toml", reloaded!.Files.Single().Path);
    }
}
