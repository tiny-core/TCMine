using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
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
    public async Task AddFilesAsync_insere_o_lote_sem_reanexar_o_grafo()
    {
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        await repo.AddVersionAsync(version, CancellationToken.None);

        await repo.AddFilesAsync(version.Id,
            [Arquivo(version.Id, "mods/a.jar", "a"), Arquivo(version.Id, "mods/b.jar", "b")],
            CancellationToken.None);

        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Equal(2, reloaded!.Files.Count);
    }

    [Fact]
    public async Task SaveVersionStateAsync_grava_o_estado_sem_duplicar_arquivos_ja_inseridos()
    {
        // Regressão do caminho da ingestão: os arquivos entram por AddFilesAsync
        // enquanto baixa e, no fecho, a versão é gravada com o grafo em memória
        // ainda contendo esses arquivos. Se eles não forem marcados Unchanged, o
        // EF tenta inseri-los de novo e a gravação explode (ou duplica).
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        await repo.AddVersionAsync(version, CancellationToken.None);

        var file = Arquivo(version.Id, "mods/a.jar", "a");
        version.UpsertFile(file);
        await repo.AddFilesAsync(version.Id, [file], CancellationToken.None);

        version.MarkResolving();
        version.ReturnToDraft();
        await repo.SaveVersionStateAsync(version, CancellationToken.None);

        var reloaded = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        Assert.Single(reloaded!.Files);
        Assert.Equal(ModpackVersionState.Draft, reloaded.State);
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

    [Fact]
    public async Task ListModInventoryAsync_marca_orfao_o_mod_que_so_vive_em_versao_arquivada()
    {
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        // Uma versão publicada e depois arquivada, e outra ativa.
        var arquivada = NovaVersao(modpack.Id, "1.0");
        arquivada.UpsertFile(Arquivo(arquivada.Id, "mods/velho.jar", "velho"));
        arquivada.UpsertFile(Arquivo(arquivada.Id, "mods/jei.jar", "jei"));
        arquivada.MarkResolving();
        arquivada.MarkReady();
        arquivada.Archive();
        await repo.AddVersionAsync(arquivada, CancellationToken.None);

        var ativa = NovaVersao(modpack.Id, "2.0");
        ativa.UpsertFile(Arquivo(ativa.Id, "mods/jei.jar", "jei"));
        ativa.UpsertFile(
            Arquivo(ativa.Id, "config/x.toml", "override:config/x.toml", ModFileOrigin.Override));
        await repo.AddVersionAsync(ativa, CancellationToken.None);

        var inventario = await repo.ListModInventoryAsync(
            new ModInventoryQuery(new PageRequest(0, 25)), CancellationToken.None);

        // Override não é mod: contá-lo encheria a tela de milhares de linhas.
        Assert.Equal(2, inventario.TotalCount);

        var velho = inventario.Items.Single(e => e.ProjectSlug == "velho");
        Assert.True(velho.IsOrphan);
        Assert.Equal(["Teste"], velho.Modpacks);

        // O jei segue vivo na 2.0: duas referências, uma ativa.
        var jei = inventario.Items.Single(e => e.ProjectSlug == "jei");
        Assert.False(jei.IsOrphan);
        Assert.Equal(1, jei.ActiveReferences);
        Assert.Equal(2, jei.TotalReferences);
    }

    [Fact]
    public async Task Consultas_paginadas_recortam_no_banco_e_devolvem_o_total()
    {
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        for (var i = 0; i < 30; i++)
            version.UpsertFile(Arquivo(version.Id, $"mods/mod{i:D2}.jar", $"mod{i:D2}"));

        // Override não conta como mod nem no total nem na página.
        version.UpsertFile(
            Arquivo(version.Id, "config/x.toml", "override:config/x.toml", ModFileOrigin.Override));

        await repo.AddVersionAsync(version, CancellationToken.None);

        var pagina = await repo.ListVersionFilesAsync(
            version.Id, VersionFileScope.Mods, null, new PageRequest(1, 25), CancellationToken.None);

        // Segunda página de 30: sobram 5. O total continua sendo o de todos.
        Assert.Equal(30, pagina.TotalCount);
        Assert.Equal(5, pagina.Items.Count);

        var busca = await repo.ListVersionFilesAsync(
            version.Id, VersionFileScope.Mods, "mod1", new PageRequest(0, 25), CancellationToken.None);

        // mod10..mod19: a busca vai em SQL, não sobre a página já trazida.
        Assert.Equal(10, busca.TotalCount);
    }

    private static async Task<Modpack> SeedModpackAsync(ModpackRepository repo)
    {
        var modpack = new Modpack
        {
            Slug = "teste", Name = "Teste", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
        };
        await repo.CreateAsync(modpack, CancellationToken.None);
        return modpack;
    }

    [Fact]
    public async Task SaveVersionStateAsync_troca_a_razao_de_uma_pendencia_sem_duplicar_a_linha()
    {
        // Regressão do fluxo real de importação: o agendador grava uma pendência
        // Queued para CADA mod do pack, e a ingestão troca a razão dos que
        // falham (DistributionDenied, NoCompatibleFile). A troca criava uma
        // entidade com Id novo, o repositório a via como INSERT, e a linha
        // Queued que continuava no banco fazia o índice único
        // (ModpackVersionId, ProjectSlug) estourar com 23505 — derrubando a
        // ingestão inteira no fim, depois de baixar centenas de mods.
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        await repo.AddVersionAsync(version, CancellationToken.None);

        var agendada = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        agendada!.UpsertPending(Pendencia(agendada.Id, "jei", PendingModReason.Queued));
        await repo.SaveVersionStateAsync(agendada, CancellationToken.None);

        // A ingestão recarrega e descobre que o autor não permite redistribuir.
        var ingerida = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        ingerida!.UpsertPending(Pendencia(ingerida.Id, "jei", PendingModReason.DistributionDenied));

        await Should.NotThrowAsync(() => repo.SaveVersionStateAsync(ingerida, CancellationToken.None));

        var relida = await repo.GetVersionAsync(version.Id, CancellationToken.None);
        relida!.PendingMods.Count.ShouldBe(1);
        relida.PendingMods.Single().Reason.ShouldBe(PendingModReason.DistributionDenied);
    }

    [Fact]
    public async Task GetByIdAsync_traz_as_pendencias_mas_nao_os_arquivos()
    {
        // As duas metades importam, e por motivos opostos.
        //
        // Pendências: a tela de detalhe decide mostrar o painel que as explica
        // por HasPendingMods. Sem o Include ele era sempre falso, o painel nunca
        // renderizava, e o admin via "13 mod(s) pendentes" ao publicar sem
        // nenhuma forma de saber quais eram nem por quê.
        //
        // Arquivos: continuam de fora de propósito. Num pack importado são
        // milhares de linhas que a tela não usa — ela mostra contagens agregadas
        // — e trazê-las fazia a página levar dezenas de segundos.
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "mods/a.jar", "a"));
        version.UpsertPending(Pendencia(version.Id, "b", PendingModReason.DistributionDenied));
        await repo.AddVersionAsync(version, CancellationToken.None);

        var lido = await repo.GetByIdAsync(modpack.Id, CancellationToken.None);
        var lidaVersao = lido!.Versions.Single();

        lidaVersao.PendingMods.Count.ShouldBe(1);
        lidaVersao.HasPendingMods.ShouldBeTrue();
        lidaVersao.Files.ShouldBeEmpty("arquivos ficam de fora por performance; a tela usa contagens");
    }

    [Fact]
    public async Task Mods_e_recursos_sao_listas_complementares()
    {
        // As duas abas dividem o mesmo conjunto: o que não é recurso é mod, sem
        // sobra nem repetição. Um shaderpack aparecendo entre os mods some no
        // meio de centenas de linhas; um mod aparecendo em recursos sugere que
        // ele é enviável à mão, e não é.
        var repo = new ModpackRepository(_factory);
        var modpack = await SeedModpackAsync(repo);

        var version = NovaVersao(modpack.Id);
        version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
        version.UpsertFile(Arquivo(version.Id, "shaderpacks/complementary.zip", "shader"));
        version.UpsertFile(Arquivo(version.Id, "resourcepacks/faithful.zip", "faithful"));
        await repo.AddVersionAsync(version, CancellationToken.None);

        var mods = await repo.ListVersionFilesAsync(
            version.Id, VersionFileScope.Mods, null, new PageRequest(0, 25), CancellationToken.None);

        var recursos = await repo.ListVersionFilesAsync(
            version.Id, VersionFileScope.Assets, null, new PageRequest(0, 25), CancellationToken.None);

        mods.Items.Select(f => f.Path).ShouldBe(["mods/jei.jar"]);

        recursos.Items.Select(f => f.Path)
            .ShouldBe(["resourcepacks/faithful.zip", "shaderpacks/complementary.zip"], ignoreOrder: true);
    }

    private static PendingMod Pendencia(Guid versionId, string slug, PendingModReason reason) =>
        new()
        {
            ModpackVersionId = versionId,
            ProjectSlug = slug,
            DisplayName = slug,
            Origin = ModFileOrigin.CurseForge,
            Reason = reason
        };

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
