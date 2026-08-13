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
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v2")]);

        var result = await cenario.UseCase.HandleAsync(
            cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.True(result.Succeeded);

        var rascunho = cenario.Repo.Adicionada!;
        Assert.Equal(ModpackVersionState.Draft, rascunho.State);
        Assert.Equal("1.1.0", rascunho.Version);

        // A versão atual pode estar publicada (imutável) e pode ter servidor
        // fixado nela: nunca é tocada.
        Assert.Equal("1.0.0", cenario.Atual.Version);
        Assert.Single(cenario.Atual.Files);
    }

    [Fact]
    public async Task Mod_atualizado_nao_copia_o_jar_velho_e_vai_para_a_fila()
    {
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v2")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        // Copiar o antigo deixaria o .jar velho no rascunho até a ingestão
        // rodar — e se ela falhasse, a versão publicaria com o mod errado.
        Assert.DoesNotContain(cenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "jei");

        var item = Assert.Single(cenario.Queue.Enfileirados);
        Assert.Equal("jei", item.ProjectId);
        Assert.Equal("v2", item.FileId);
    }

    [Fact]
    public async Task Mod_que_o_admin_acrescentou_e_copiado_para_o_rascunho()
    {
        // O caso que motivou o merge inteiro: o extra do admin sobrevive a uma
        // atualização de verdade (o autor subiu o jei).
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1"), ("meu-mod", "x1")],
            delesMods: [("jei", "v2")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.Contains(cenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "meu-mod");
    }

    [Fact]
    public async Task Mod_removido_pelo_autor_nao_e_copiado()
    {
        var cenario = Cenario(
            baseMods: [("jei", "v1"), ("velho", "v1")],
            nossosMods: [("jei", "v1"), ("velho", "v1")],
            delesMods: [("jei", "v1")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.DoesNotContain(cenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "velho");
        Assert.Contains(cenario.Repo.Adicionada.Files, f => f.ProjectSlug == "jei");
    }

    [Fact]
    public async Task Em_conflito_o_mod_do_admin_permanece_e_nao_vai_para_a_fila()
    {
        // Autor e admin trocaram o mesmo mod. Aplicar o do autor apagaria a
        // escolha do admin sem ele pedir.
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v2")],
            delesMods: [("jei", "v3")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        var mantido = Assert.Single(cenario.Repo.Adicionada!.Files, f => f.ProjectSlug == "jei");
        Assert.Equal("v2", mantido.OriginReference);
        Assert.Empty(cenario.Queue.Enfileirados);
    }

    [Fact]
    public async Task Config_que_o_admin_nao_tocou_recebe_a_versao_do_autor()
    {
        var cenario = Cenario(
            baseMods: [],
            nossosMods: [],
            delesMods: [],
            baseOverrides: [("config/a.toml", "original")],
            nossosOverrides: [("config/a.toml", "original")], // intocado
            delesOverrides: [("config/a.toml", "novo do autor")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        var config = Assert.Single(cenario.Repo.Adicionada!.Files, f => f.Path == "config/a.toml");
        Assert.Equal(Sha("novo do autor"), config.Sha256);
    }

    [Fact]
    public async Task Config_que_o_admin_editou_nao_e_sobrescrito()
    {
        // Config customizado é o trabalho mais caro de refazer — é justamente o
        // que a atualização não pode comer. O mod atualizado junto existe para
        // haver rascunho: se SÓ o config divergisse e ele fosse do admin, não
        // sobraria nada a aplicar.
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v2")],
            baseOverrides: [("config/a.toml", "original")],
            nossosOverrides: [("config/a.toml", "editado por mim")],
            delesOverrides: [("config/a.toml", "novo do autor")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        var config = Assert.Single(cenario.Repo.Adicionada!.Files, f => f.Path == "config/a.toml");
        Assert.Equal(Sha("editado por mim"), config.Sha256);
    }

    [Fact]
    public async Task Atualizacao_so_de_config_conta_como_mudanca()
    {
        // Regressão: o plano só olhava mods, então um pack que publicou apenas
        // ajustes de config era recusado com "nada mudou" — e pack grande faz
        // isso o tempo todo.
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v1")],
            baseOverrides: [("config/a.toml", "original")],
            nossosOverrides: [("config/a.toml", "original")],
            delesOverrides: [("config/a.toml", "ajustado pelo autor")]);

        var result = await cenario.UseCase.HandleAsync(
            cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Plan.OverridesUpdated);
    }

    [Fact]
    public async Task Config_novo_do_autor_entra()
    {
        var cenario = Cenario(
            baseMods: [],
            nossosMods: [],
            delesMods: [],
            baseOverrides: [],
            nossosOverrides: [],
            delesOverrides: [("config/novo.toml", "conteudo")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.Contains(cenario.Repo.Adicionada!.Files, f => f.Path == "config/novo.toml");
    }

    [Fact]
    public async Task Snapshot_do_rascunho_passa_a_ser_o_da_origem_nova()
    {
        // Sem isto, a atualização seguinte compararia contra a base velha e
        // acusaria como "mudança do admin" tudo o que o autor fez agora.
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v2")]);

        await cenario.UseCase.HandleAsync(cenario.Atual.Id, "1.1.0", CancellationToken.None);

        var snapshot = UpstreamSnapshot.FromJson(cenario.Repo.Adicionada!.UpstreamSnapshotJson);
        Assert.NotNull(snapshot);
        Assert.Equal("v2", snapshot!.Mods["jei"]);

        // E a procedência da release, para o "4.2 → 4.3" da próxima vez.
        Assert.Equal("file-novo", cenario.Repo.Adicionada.UpstreamFileId);
    }

    [Fact]
    public async Task DryRun_devolve_o_plano_sem_gravar_nada()
    {
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v2")]);

        var result = await cenario.UseCase.HandleAsync(
            cenario.Atual.Id, "", CancellationToken.None, true);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Plan.Update);
        Assert.Null(result.Value.DraftId);

        // Nada gravado: é o que permite a tela mostrar o diff antes de o admin
        // decidir.
        Assert.Null(cenario.Repo.Adicionada);
        Assert.Empty(cenario.Queue.Enfileirados);
    }

    [Fact]
    public async Task Recusa_atualizar_versao_sem_retrato_da_origem()
    {
        // Sem a base não há como distinguir "o autor mexeu" de "o admin mexeu",
        // e o merge viraria um "sobrescreve tudo" disfarçado.
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v2")]);

        cenario.Atual.UpstreamSnapshotJson = null;

        var result = await cenario.UseCase.HandleAsync(
            cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(cenario.Repo.Adicionada);
    }

    [Fact]
    public async Task Recusa_quando_nada_mudou()
    {
        var cenario = Cenario(
            baseMods: [("jei", "v1")],
            nossosMods: [("jei", "v1")],
            delesMods: [("jei", "v1")]);

        var result = await cenario.UseCase.HandleAsync(
            cenario.Atual.Id, "1.1.0", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(cenario.Repo.Adicionada);
    }

    // ---- Fixtures ----

    private static string Sha(string conteudo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo)));

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
        var queue = new FakeQueue();

        var useCase = new UpdateFromUpstream(
            [new FakeSource(pack)], repo, new FakeBlobStore(), queue, new FakeJobProgress());

        return new Contexto(useCase, repo, queue, atual);
    }

    private sealed record Contexto(
        UpdateFromUpstream UseCase, FakeRepo Repo, FakeQueue Queue, ModpackVersion Atual);

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

    private sealed class FakeQueue : IIngestionQueue
    {
        public List<ModIngestionItem> Enfileirados { get; } = [];

        public ValueTask EnqueueAsync(Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
        {
            Enfileirados.AddRange(items);
            return ValueTask.CompletedTask;
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
