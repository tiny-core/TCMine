using System.Security.Cryptography;
using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     A montagem do rascunho de atualização: o que copia, o que fica de fora e
///     como os configs são tratados.
///     O <see cref="UpstreamMerge" /> já tem testes próprios — aqui é o outro
///     lado, onde o plano vira arquivos de verdade. Errar aqui apaga trabalho do
///     admin com o plano perfeitamente correto.
/// </summary>
public sealed class UpdateFromUpstreamTests
{
    [Fact]
    public async Task Cria_rascunho_novo_sem_tocar_na_versao_atual()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.True(result.Succeeded);

        var rascunho = scenario.Repo.Adicionada!;
        Assert.Equal(ModpackVersionState.Draft, rascunho.State);
        Assert.Equal("1.1.0", rascunho.Version);

        // A versão atual pode estar publicada (imutável) e pode ter servidor
        // fixado nela: nunca é tocada.
        Assert.Equal("1.0.0", scenario.Atual.Version);
        Assert.Single(scenario.Atual.Files);
    }

    [Fact]
    public async Task Mod_atualizado_nao_copia_o_jar_velho_e_vai_para_a_fila()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        // Copiar o antigo deixaria o .jar velho no rascunho até a ingestão
        // rodar — e se ela falhasse, a versão publicaria com o mod errado.
        Assert.DoesNotContain(scenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "jei");

        var item = Assert.Single(scenario.Queue.Enfileirados);
        Assert.Equal("jei", item.ProjectId);
        Assert.Equal("v2", item.FileId);
    }

    [Fact]
    public async Task Mod_que_o_admin_acrescentou_e_copiado_para_o_rascunho()
    {
        // O caso que motivou o merge inteiro: o extra do admin sobrevive a uma
        // atualização de verdade (o autor subiu o jei).
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1"), ("meu-mod", "x1")],
            [("jei", "v2")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.Contains(scenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "meu-mod");
    }

    [Fact]
    public async Task Mod_removido_pelo_autor_nao_e_copiado()
    {
        var scenario = Cenario(
            [("jei", "v1"), ("velho", "v1")],
            [("jei", "v1"), ("velho", "v1")],
            [("jei", "v1")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.DoesNotContain(scenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "velho");
        Assert.Contains(scenario.Repo.Adicionada.Files, f => f.ProjectSlug == "jei");
    }

    [Fact]
    public async Task Em_conflito_o_mod_do_admin_permanece_e_nao_vai_para_a_fila()
    {
        // Autor e admin trocaram o mesmo mod. Aplicar o do autor apagaria a
        // escolha do admin sem ele pedir. O emi existe para haver rascunho: só
        // com o conflito desmarcado não sobraria nada a aplicar.
        var scenario = Cenario(
            [("jei", "v1"), ("emi", "e1")],
            [("jei", "v2"), ("emi", "e1")],
            [("jei", "v3"), ("emi", "e2")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        var mantido = Assert.Single(scenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "jei");
        Assert.Equal("v2", mantido.OriginReference);

        var item = Assert.Single(scenario.Queue.Enfileirados);
        Assert.Equal("emi", item.ProjectId);
    }

    [Fact]
    public async Task Config_que_o_admin_nao_tocou_recebe_a_versao_do_autor()
    {
        var scenario = Cenario(
            [],
            [],
            [],
            [("config/a.toml", "original")],
            [("config/a.toml", "original")], // intocado
            [("config/a.toml", "novo do autor")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        var config = Assert.Single(scenario.Repo.Adicionada!.Files, f => f.Path == "config/a.toml");
        Assert.Equal(Sha("novo do autor"), config.Sha256);
    }

    [Fact]
    public async Task Config_que_o_admin_editou_nao_e_sobrescrito()
    {
        // Config customizado é o trabalho mais caro de refazer — é justamente o
        // que a atualização não pode comer. O mod atualizado junto existe para
        // haver rascunho: se SÓ o config divergisse e ele fosse do admin, não
        // sobraria nada a aplicar.
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")],
            [("config/a.toml", "original")],
            [("config/a.toml", "editado por mim")],
            [("config/a.toml", "novo do autor")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        var config = Assert.Single(scenario.Repo.Adicionada!.Files, f => f.Path == "config/a.toml");
        Assert.Equal(Sha("editado por mim"), config.Sha256);
    }

    [Fact]
    public async Task Atualizacao_so_de_config_conta_como_mudanca()
    {
        // Regressão: o plano só olhava mods, então um pack que publicou apenas
        // ajustes de config era recusado com "nada mudou" — e pack grande faz
        // isso o tempo todo.
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v1")],
            [("config/a.toml", "original")],
            [("config/a.toml", "original")],
            [("config/a.toml", "ajustado pelo autor")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Plan.Overrides.Updated);
    }

    [Fact]
    public async Task Config_removido_pelo_autor_sai_do_rascunho()
    {
        // Sem isto, um pack que reorganiza a pasta config/ acumula lixo a cada
        // atualização — e o launcher instala esse lixo na máquina do jogador.
        var scenario = Cenario(
            [],
            [],
            [],
            [("config/velho.toml", "original"), ("config/fica.toml", "x")],
            [("config/velho.toml", "original"), ("config/fica.toml", "x")],
            [("config/fica.toml", "x")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Plan.Overrides.Removed);

        var rascunho = scenario.Repo.Adicionada!;
        Assert.DoesNotContain(rascunho.Files, f => f.Path == "config/velho.toml");
        Assert.Contains(rascunho.Files, f => f.Path == "config/fica.toml");
    }

    [Fact]
    public async Task Config_removido_pelo_autor_mas_editado_pelo_admin_permanece()
    {
        // Remover a customização do admin seria a única coisa pior que ignorá-la.
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")],
            [("config/meu.toml", "original")],
            [("config/meu.toml", "editado por mim")],
            []);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        var rascunho = scenario.Repo.Adicionada!;
        var config = Assert.Single(rascunho.Files, f => f.Path == "config/meu.toml");
        Assert.Equal(Sha("editado por mim"), config.Sha256);
    }

    [Fact]
    public async Task Remocao_so_de_config_conta_como_mudanca()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v1")],
            [("config/velho.toml", "original")],
            [("config/velho.toml", "original")],
            []);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Config_novo_do_autor_entra()
    {
        var scenario = Cenario(
            [],
            [],
            [],
            [],
            [],
            [("config/novo.toml", "conteudo")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.Contains(scenario.Repo.Adicionada!.Files, f => f.Path == "config/novo.toml");
    }

    [Fact]
    public async Task Snapshot_do_rascunho_passa_a_ser_o_da_origem_nova()
    {
        // Sem isto, a atualização seguinte compararia contra a base velha e
        // acusaria como "mudança do admin" tudo o que o autor fez agora.
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")]);

        await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        var snapshot = UpstreamSnapshot.FromJson(scenario.Repo.Adicionada!.UpstreamSnapshotJson);
        Assert.NotNull(snapshot);
        Assert.Equal("v2", snapshot!.Mods["jei"]);

        // E a procedência da release, para o "4.2 → 4.3" da próxima vez.
        Assert.Equal("file-novo", scenario.Repo.Adicionada.UpstreamFileId);
    }

    [Fact]
    public async Task DryRun_devolve_o_plano_sem_gravar_nada()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "", CancellationToken.None, true);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Plan.Update);
        Assert.Null(result.Value.DraftId);

        // Nada gravado: é o que permite a tela mostrar o diff antes de o admin
        // decidir.
        Assert.Null(scenario.Repo.Adicionada);
        Assert.Empty(scenario.Queue.Enfileirados);
    }

    [Fact]
    public async Task Recusa_atualizar_versao_sem_retrato_da_origem()
    {
        // Sem a base não há como distinguir "o autor mexeu" de "o admin mexeu",
        // e o merge viraria um "sobrescreve tudo" disfarçado.
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")]);

        scenario.Atual.UpstreamSnapshotJson = null;

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(scenario.Repo.Adicionada);
    }

    [Fact]
    public async Task Recusa_quando_nada_mudou()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v1")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(scenario.Repo.Adicionada);
    }

    // ---- Escolha item a item ----

    [Fact]
    public async Task Conflito_marcado_baixa_o_mod_do_autor_no_lugar_do_seu()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v2")],
            [("jei", "v3")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None, selection: Marcados(UpstreamItemKey.Mod("jei")));

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(scenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "jei");
        var item = Assert.Single(scenario.Queue.Enfileirados);
        Assert.Equal("v3", item.FileId);
    }

    [Fact]
    public async Task Atualizacao_desmarcada_mantem_o_jar_atual_e_nao_baixa()
    {
        var scenario = Cenario(
            [("jei", "v1"), ("emi", "e1")],
            [("jei", "v1"), ("emi", "e1")],
            [("jei", "v2"), ("emi", "e2")]);

        await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None, selection: Marcados(UpstreamItemKey.Mod("emi")));

        var jei = Assert.Single(scenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "jei");
        Assert.Equal("v1", jei.OriginReference);

        var item = Assert.Single(scenario.Queue.Enfileirados);
        Assert.Equal("emi", item.ProjectId);
    }

    [Fact]
    public async Task Config_editado_marcado_recebe_o_conteudo_do_autor()
    {
        var scenario = Cenario(
            [],
            [],
            [],
            [("config/a.toml", "original")],
            [("config/a.toml", "editado por mim")],
            [("config/a.toml", "novo do autor")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None,
            selection: Marcados(UpstreamItemKey.Override("config/a.toml")));

        Assert.True(result.Succeeded);
        var config = Assert.Single(scenario.Repo.Adicionada!.Files, f => f.Path == "config/a.toml");
        Assert.Equal(Sha("novo do autor"), config.Sha256);
    }

    [Fact]
    public async Task Nada_marcado_nao_cria_rascunho()
    {
        var scenario = Cenario(
            [("jei", "v1")],
            [("jei", "v1")],
            [("jei", "v2")]);

        var result = await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None, selection: Marcados());

        Assert.False(result.Succeeded);
        Assert.Null(scenario.Repo.Adicionada);
    }

    [Fact]
    public async Task So_conflito_de_config_sem_escolha_nao_cria_rascunho()
    {
        // Antes era "o que o autor mudou, você já tinha personalizado"; agora o
        // conflito vai para a tela, desmarcado — e sem marcar, não há o que criar.
        var scenario = Cenario(
            [],
            [],
            [],
            [("config/a.toml", "original")],
            [("config/a.toml", "editado por mim")],
            [("config/a.toml", "novo do autor")]);

        var result = await scenario.UseCase.HandleAsync(scenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(scenario.Repo.Adicionada);
    }

    [Fact]
    public async Task Recusado_vira_escolha_do_admin_no_retrato()
    {
        // O retrato é o pack do autor inteiro, mesmo com a atualização recusada:
        // na próxima vez o jar velho conta como escolha do admin, e não como
        // "atualização pendente" que reaparece marcada.
        var scenario = Cenario(
            [("jei", "v1"), ("emi", "e1")],
            [("jei", "v1"), ("emi", "e1")],
            [("jei", "v2"), ("emi", "e2")]);

        await scenario.UseCase.HandleAsync(
            scenario.Atual.Id, "1.1.0", CancellationToken.None, selection: Marcados(UpstreamItemKey.Mod("emi")));

        var snapshot = UpstreamSnapshot.FromJson(scenario.Repo.Adicionada!.UpstreamSnapshotJson)!;
        Assert.Equal("v2", snapshot.Mods["jei"]);
    }

    [Fact]
    public async Task DryRun_traz_o_lado_do_autor_dos_configs_em_conflito()
    {
        var scenario = Cenario(
            [],
            [],
            [],
            [("config/a.toml", "original"), ("config/b.toml", "original")],
            [("config/a.toml", "editado por mim"), ("config/b.toml", "original")],
            [("config/a.toml", "novo do autor"), ("config/b.toml", "mudou")]);

        var result = await scenario.UseCase.HandleAsync(scenario.Atual.Id, "", CancellationToken.None, true);

        // Só o conflito: o config intocado (b) não precisa dos dois lados.
        var (path, conteudo) = Assert.Single(result.Value!.TheirConflictContents);
        Assert.Equal("config/a.toml", path);
        Assert.Equal("novo do autor", Encoding.UTF8.GetString(conteudo));
    }

    // ---- Fixtures ----

    private static UpstreamSelection Marcados(params string[] keys) =>
        new(keys.ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static string Sha(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static Contexto Cenario(
        (string Slug, string File)[] baseMods,
        (string Slug, string File)[] nossosMods,
        (string Slug, string File)[] delesMods,
        (string Path, string Content)[]? baseOverrides = null,
        (string Path, string Content)[]? nossosOverrides = null,
        (string Path, string Content)[]? delesOverrides = null)
    {
        var modpack = new Modpack
        {
            Name = "Pack",
            Slug = "pack",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge,
            UpstreamProvider = ModFileOrigin.CurseForge,
            UpstreamProjectId = "999"
        };

        var atual = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = "1.0.0",
            LoaderVersion = "21.1.100",
            UpstreamFileId = "file-antigo",
            UpstreamSnapshotJson = new UpstreamSnapshot
            {
                Mods = baseMods.ToDictionary(m => m.Slug, m => m.File),
                Overrides = (baseOverrides ?? []).ToDictionary(o => o.Path, o => Sha(o.Content))
            }.ToJson()
        };

        foreach (var (slug, fileId) in nossosMods)
        {
            atual.UpsertFile(new ModpackFile
            {
                ModpackVersionId = atual.Id,
                ProjectSlug = slug,
                Path = $"mods/{slug}.jar",
                Sha256 = Sha(slug + fileId),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.CurseForge,
                OriginReference = fileId
            });
        }

        foreach (var (path, content) in nossosOverrides ?? [])
        {
            atual.UpsertFile(new ModpackFile
            {
                ModpackVersionId = atual.Id,
                ProjectSlug = $"override:{path}",
                Path = path,
                Sha256 = Sha(content),
                SizeBytes = content.Length,
                Side = FileSide.Both,
                Origin = ModFileOrigin.Override
            });
        }

        var pack = new UpstreamPack
        {
            ProjectId = "999",
            FileId = "file-novo",
            VersionLabel = "4.3",
            Name = "Pack",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge,
            LoaderVersion = "21.1.100",
            Mods = [.. delesMods.Select(m => new UpstreamPackMod(m.Slug, m.File, true, m.Slug))],
            Overrides =
            [
                .. (delesOverrides ?? []).Select(o =>
                    new UpstreamPackOverride(o.Path, Encoding.UTF8.GetBytes(o.Content)))
            ]
        };

        var repo = new FakeRepo(modpack, atual);
        // O rascunho é a versão que o caso de uso acabou de criar.
        var queue = new FakeIngestionQueue(_ => repo.Adicionada);

        var useCase = new UpdateFromUpstream(
            [new FakeSource(pack)], repo, new FakeBlobStore(), new IngestionScheduler(repo, queue),
            new FakeJobProgress());

        return new Contexto(useCase, repo, queue, atual);
    }

    private sealed record Contexto(
        UpdateFromUpstream UseCase,
        FakeRepo Repo,
        FakeIngestionQueue Queue,
        ModpackVersion Atual);

    // ---- Fakes ----

    private sealed class FakeSource(UpstreamPack pack) : FakeUpstreamPackSourceBase
    {
        public override Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct) =>
            Task.FromResult<UpstreamPack?>(pack);
    }

    /// <summary>Hash real do conteúdo: é o que faz a regra de três vias dos overrides valer.</summary>
    private sealed class FakeBlobStore : FakeBlobStoreBase
    {
        public override async Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
        }
    }

    private sealed class FakeRepo(Modpack modpack, ModpackVersion atual) : FakeModpackRepositoryBase
    {
        public ModpackVersion? Adicionada { get; private set; }

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(modpack);

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(atual);

        public override Task AddVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            Adicionada = version;
            return Task.CompletedTask;
        }
    }
}
