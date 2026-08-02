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
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "config/a.toml", "override:config/a.toml", ModFileOrigin.Override));
        await repo.AddVersionAsync(version, CancellationToken.None);

        // Recarrega, edita o path IN-PLACE e regrava (o que o move faz).
        var toEdit = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        toEdit!.Files.Single().Path = "backup/a.toml";
        await repo.UpdateVersionAsync(toEdit, CancellationToken.None);

        // Relê de um contexto novo: a mudança tem de estar no banco.
        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Equal("backup/a.toml", reloaded!.Files.Single().Path);
    }

    [Fact]
    public async Task UpdateVersionAsync_insere_arquivo_novo_adicionado_a_versao()
    {
        // Caminho "Added": um arquivo com Id que o EF ainda não conhece tem de
        // virar INSERT, não ser ignorado.
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
        await repo.AddVersionAsync(version, CancellationToken.None);

        var toEdit = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        toEdit!.UpsertFile(Arquivo(toEdit.Id, "mods/create.jar", "create"));
        await repo.UpdateVersionAsync(toEdit, CancellationToken.None);

        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Equal(2, reloaded!.Files.Count);
        Assert.Contains(reloaded.Files, f => f.ProjectSlug == "create");
    }

    [Fact]
    public async Task UpdateVersionAsync_sozinho_nao_apaga_arquivo_removido_da_colecao()
    {
        // Contrato do §8: Update num grafo destacado NÃO cascateia a deleção de
        // filhos tirados da coleção — quem remove um arquivo deve chamar
        // RemoveFileAsync explicitamente. Este teste trava esse comportamento
        // para ninguém assumir o contrário e criar um bug silencioso.
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
        await repo.AddVersionAsync(version, CancellationToken.None);

        var toEdit = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        toEdit!.Files.Clear(); // tira o arquivo da coleção
        await repo.UpdateVersionAsync(toEdit, CancellationToken.None);

        // O arquivo continua no banco — Update não o apagou.
        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Single(reloaded!.Files);
    }

    [Fact]
    public async Task RemoveFileAsync_remove_apenas_o_arquivo_alvo()
    {
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
        version.UpsertFile(Arquivo(version.Id, "mods/create.jar", "create"));
        await repo.AddVersionAsync(version, CancellationToken.None);

        var jei = (await repo.GetVersionAsync(version.Id, CancellationToken.None))!
            .Files.Single(f => f.ProjectSlug == "jei");
        await repo.RemoveFileAsync(version.Id, jei.Id, CancellationToken.None);

        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Single(reloaded!.Files);
        Assert.Equal("create", reloaded.Files.Single().ProjectSlug);
    }

    [Fact]
    public async Task RemoveVersionAsync_cascateia_para_os_arquivos_e_preserva_a_outra_versao()
    {
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var v1 = NovaVersao(modpack.Id);
        v1.UpsertFile(Arquivo(v1.Id, "mods/jei.jar", "jei"));
        await repo.AddVersionAsync(v1, CancellationToken.None);

        var v2 = NovaVersao(modpack.Id, "2.0");
        v2.UpsertFile(Arquivo(v2.Id, "mods/create.jar", "create"));
        await repo.AddVersionAsync(v2, CancellationToken.None);

        await repo.RemoveVersionAsync(v1.Id, CancellationToken.None);

        Assert.Null(await repo.GetVersionAsync(v1.Id, CancellationToken.None));
        var survivor = await repo.GetVersionAsync(v2.Id, CancellationToken.None);
        Assert.NotNull(survivor);
        Assert.Equal("create", survivor!.Files.Single().ProjectSlug);
    }

    [Fact]
    public async Task RemoveAsync_cascateia_para_versoes_e_arquivos()
    {
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
        await repo.AddVersionAsync(version, CancellationToken.None);

        await repo.RemoveAsync(modpack.Id, CancellationToken.None);

        Assert.Null(await repo.GetByIdAsync(modpack.Id, CancellationToken.None));
        Assert.Null(await repo.GetVersionAsync(version.Id, CancellationToken.None));
    }

    // ---------- Helpers ----------

    private static async Task<Modpack> SeedModpackAsync(ModpackRepository repo)
    {
        var modpack = new Modpack
        {
            Slug = "teste", Name = "Teste", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
        };
        await repo.CreateAsync(modpack, CancellationToken.None);
        return modpack;
    }

    private static ModpackVersion NovaVersao(Guid modpackId, string version = "1.0") =>
        new() { ModpackId = modpackId, Version = version, LoaderVersion = "21.1.234" };

    private static ModpackFile Arquivo(
        Guid versionId, string path, string slug, ModFileOrigin origin = ModFileOrigin.Modrinth) =>
        new()
        {
            ModpackVersionId = versionId,
            Path = path,
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = origin,
            ProjectSlug = slug
        };
}
