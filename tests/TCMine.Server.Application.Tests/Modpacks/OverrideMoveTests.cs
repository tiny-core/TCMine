using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class OverrideMoveTests
{
    [Fact]
    public async Task Move_arquivo_muda_o_path_e_a_identidade()
    {
        var (version, repo, undo) = Setup(Override("config/a.toml"));
        var move = new MoveOverride(repo, undo);

        var result = await move.HandleAsync(version.Id, "config/a.toml", "backup/a.toml", CancellationToken.None);

        Assert.True(result.Succeeded);
        var file = version.Files.Single();
        Assert.Equal("backup/a.toml", file.Path);
        Assert.Equal("override:backup/a.toml", file.ProjectSlug); // identidade acompanha
    }

    [Fact]
    public async Task Move_pasta_remapeia_todos_os_filhos_preservando_estrutura()
    {
        var (version, repo, undo) = Setup(
            Override("config/mod/a.toml"),
            Override("config/mod/sub/b.toml"));
        var move = new MoveOverride(repo, undo);

        // Move a pasta "config/mod" para "backup/mod".
        var result = await move.HandleAsync(version.Id, "config/mod", "backup/mod", CancellationToken.None);

        Assert.True(result.Succeeded);
        var paths = version.Files.Select(f => f.Path).OrderBy(p => p).ToArray();
        Assert.Equal(["backup/mod/a.toml", "backup/mod/sub/b.toml"], paths);
    }

    [Fact]
    public async Task Move_recusa_quando_o_destino_ja_existe()
    {
        var (version, repo, undo) = Setup(
            Override("config/a.toml"),
            Override("backup/a.toml")); // destino ocupado
        var move = new MoveOverride(repo, undo);

        var result = await move.HandleAsync(version.Id, "config/a.toml", "backup/a.toml", CancellationToken.None);

        Assert.False(result.Succeeded);
        // Nada mudou — o original continua no sítio.
        Assert.Contains(version.Files, f => f.Path == "config/a.toml");
    }

    [Fact]
    public async Task Move_so_e_permitido_em_rascunho()
    {
        var (version, repo, undo) = Setup(Override("config/a.toml"));
        Publish(version); // Draft → Resolving → Ready
        var move = new MoveOverride(repo, undo);

        var result = await move.HandleAsync(version.Id, "config/a.toml", "backup/a.toml", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("config/a.toml", version.Files.Single().Path);
    }

    [Fact]
    public async Task Undo_restaura_o_path_anterior()
    {
        var (version, repo, undo) = Setup(Override("config/a.toml"));
        var move = new MoveOverride(repo, undo);
        var undoMove = new UndoOverrideMove(repo, undo);

        await move.HandleAsync(version.Id, "config/a.toml", "backup/a.toml", CancellationToken.None);
        Assert.Equal("backup/a.toml", version.Files.Single().Path); // moveu

        var result = await undoMove.HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("config/a.toml", version.Files.Single().Path); // voltou
    }

    [Fact]
    public async Task Undo_sem_historico_falha()
    {
        var (version, repo, undo) = Setup(Override("config/a.toml"));
        var undoMove = new UndoOverrideMove(repo, undo);

        var result = await undoMove.HandleAsync(version.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    // ---- Fixtures ----

    private static (ModpackVersion, FakeRepo, OverrideUndoService) Setup(params ModpackFile[] files)
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0", LoaderVersion = "21.1.234"
        };
        foreach (var f in files)
            version.UpsertFile(f);

        return (version, new FakeRepo(version), new OverrideUndoService());
    }

    private static ModpackFile Override(string path)
    {
        return new ModpackFile
        {
            ModpackVersionId = Guid.Empty,
            Path = path,
            Sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Override,
            ProjectSlug = $"override:{path}"
        };
    }

    private static void Publish(ModpackVersion v)
    {
        v.MarkResolving();
        v.MarkReady();
    }

    // Repositório fake: devolve sempre a MESMA instância da versão, então as
    // mudanças do move ficam visíveis nas asserções (in-place, como o EF faria).
    private sealed class FakeRepo(ModpackVersion version) : IModpackRepository
    {
        public Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => throw new NotImplementedException();

        public Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

        public Task CreateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();

        public Task AddVersionAsync(ModpackVersion v, CancellationToken ct) => throw new NotImplementedException();

        public Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task UpdateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();

        public Task RemoveVersionAsync(Guid versionId, CancellationToken ct) => throw new NotImplementedException();
    }
}
