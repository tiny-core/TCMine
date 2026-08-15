using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

using TCMine.Server.Application.Tests.Fakes;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class ModpackIngestionServiceTests
{
    [Fact]
    public async Task Puxa_dependencias_requeridas_e_ignora_opcionais()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // A precisa de B (requerida) e C (opcional). B precisa de D (requerida).
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>>
        {
            ["A"] = [Req("B"), Opt("C")], ["B"] = [Req("D")], ["C"] = [], ["D"] = []
        });

        var service = NewService(repo, resolver);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        var slugs = version.Files.Select(f => f.ProjectSlug!).OrderBy(s => s).ToArray();
        Assert.Equal(["A", "B", "D"], slugs); // opcional C fora; transitiva D dentro
        Assert.Equal(ModpackVersionState.Draft, version.State);
    }

    [Fact]
    public async Task Nao_entra_em_loop_com_dependencia_circular()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>>
        {
            ["A"] = [Req("B")], ["B"] = [Req("A")] // ciclo
        });

        var service = NewService(repo, resolver);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        Assert.Equal(2, version.Files.Count);
        Assert.Equal(ModpackVersionState.Draft, version.State);
    }

    [Fact]
    public async Task Mod_que_exige_loader_mais_novo_vira_pendencia_em_vez_de_instalar()
    {
        // Este mod baixa sem erro e instala sem reclamar — e derruba o servidor
        // no arranque, com uma exceção do loader que não aponta para o TCMine.
        var version = NewDraftVersion(); // LoaderVersion = 21.1.234
        var repo = new FakeModpackRepository { Version = version };
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>> { ["A"] = [] });

        var service = new ModpackIngestionService(
            repo, new FakeBlobStore(), [resolver], new FakeDownloader(),
            new FakeJarInspector("[21.2.0,)"), new FakeJobProgress(),
            NullLogger<ModpackIngestionService>.Instance);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        Assert.Empty(version.Files);

        var pendente = Assert.Single(version.PendingMods);
        Assert.Contains("21.2.0", pendente.Detail);
        Assert.Contains("21.1.234", pendente.Detail);
    }

    [Fact]
    public async Task Mod_com_exigencia_satisfeita_instala_normalmente()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>> { ["A"] = [] });

        var service = new ModpackIngestionService(
            repo, new FakeBlobStore(), [resolver], new FakeDownloader(),
            new FakeJarInspector("[21.1.0,)"), new FakeJobProgress(),
            NullLogger<ModpackIngestionService>.Instance);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        Assert.Single(version.Files);
        Assert.Empty(version.PendingMods);
    }

    [Fact]
    public async Task Mod_sem_arquivo_compativel_vira_pendencia_sem_reprovar_a_versao()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // A precisa de X, que o resolver não encontra.
        var resolver = new FakeResolver(
            new Dictionary<string, IReadOnlyList<ModDependency>> { ["A"] = [Req("X")] },
            ["X"]);

        var service = NewService(repo, resolver);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        // Antes isto reprovava a versão inteira. Num pack de centenas de mods
        // uma dúzia de indisponíveis é a norma, e reprovar deixava o pack
        // eternamente impublicável — agora fica registrado para upload manual.
        Assert.Equal(ModpackVersionState.Draft, version.State);

        var pendente = Assert.Single(version.PendingMods);
        Assert.Equal("X", pendente.ProjectSlug);
        Assert.Equal(PendingModReason.NoCompatibleFile, pendente.Reason);
    }

    // ---- Fixtures ----

    private static ModpackIngestionService NewService(
        FakeModpackRepository repo, FakeResolver resolver, FakeJobProgress? progress = null)
    {
        return new ModpackIngestionService(repo, new FakeBlobStore(), [resolver], new FakeDownloader(),
            new FakeJarInspector(), progress ?? new FakeJobProgress(), NullLogger<ModpackIngestionService>.Instance);
    }

    [Fact]
    public async Task Grava_em_lotes_enquanto_baixa_em_vez_de_so_no_fim()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // 60 mods: com lote de 25, dá dois lotes cheios e a sobra no fecho.
        var ids = Enumerable.Range(1, 60).Select(i => $"m{i}").ToArray();
        var resolver = new FakeResolver(ids.ToDictionary(id => id, _ => (IReadOnlyList<ModDependency>)[]));

        await NewService(repo, resolver).IngestAsync(
            version.Id, [.. ids.Select(Item)], CancellationToken.None);

        // Gravar só no fim mostrava "0 mods" durante a importação inteira e
        // perdia tudo se o processo caísse no meio.
        Assert.Equal([25, 25, 10], repo.Lotes);
    }

    [Fact]
    public async Task Estado_final_e_gravado_antes_de_anunciar_a_conclusao()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>> { ["A"] = [] });

        var progress = new FakeJobProgress();
        var salvos = 0;
        ModpackVersionState? estadoNoAviso = null;

        // O aviso de conclusão faz a tela recarregar do banco. Se ele sair antes
        // do save, ela lê "Resolvendo" e fica presa numa barra parada — o job já
        // saiu do registro, então nem progresso chega mais. Só um F5 destrava.
        progress.OnComplete = () =>
        {
            salvos = repo.SaveCount;
            estadoNoAviso = version.State;
        };

        await NewService(repo, resolver, progress).IngestAsync(
            version.Id, [Item("A")], CancellationToken.None);

        Assert.Equal(ModpackVersionState.Draft, estadoNoAviso);
        Assert.Equal(repo.SaveCount, salvos);
    }

    [Fact]
    public async Task Lado_declarado_pela_origem_ganha_do_lado_pedido()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // O Modrinth diz que este mod não roda no servidor.
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>> { ["A"] = [] })
        {
            SideByProject = { ["A"] = FileSide.ClientOnly }
        };

        await NewService(repo, resolver).IngestAsync(
            version.Id,
            [new ModIngestionItem(ModFileOrigin.Modrinth, "A", null, FileSide.Both)],
            CancellationToken.None);

        // Supor Both quando a origem sabe o lado colocaria um mod de cliente
        // dentro do servidor de jogo.
        var file = Assert.Single(version.Files);
        Assert.Equal(FileSide.ClientOnly, file.Side);
    }

    [Fact]
    public async Task Erro_inesperado_encerra_o_acompanhamento_e_marca_a_versao()
    {
        // Regressão dupla: antes, uma exceção no meio do laço deixava a versão
        // presa em "Resolvendo" para sempre E a barra de progresso girando —
        // além de nunca chegar às dependências ainda não descobertas.
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };
        var progress = new FakeJobProgress();

        var service = new ModpackIngestionService(
            repo, new FakeBlobStore(), [new ExplodeResolver()], new FakeDownloader(),
            new FakeJarInspector(), progress, NullLogger<ModpackIngestionService>.Instance);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        Assert.Equal(ModpackVersionState.Failed, version.State);

        var (_, erro) = Assert.Single(progress.Completed);
        Assert.NotNull(erro);
    }

    [Fact]
    public async Task Total_do_progresso_nao_sobe_com_dependencias_transitivas()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // Um mod pedido que arrasta duas dependências.
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>>
        {
            ["A"] = [Req("B"), Req("C")]
        });

        var progress = new FakeJobProgress();
        await NewService(repo, resolver, progress).IngestAsync(
            version.Id, [Item("A")], CancellationToken.None);

        // O bug era o denominador crescer enquanto baixava ("88/567" virando
        // "230/629"), o que faz a barra andar para trás e não informa nada.
        Assert.All(progress.Reported, r => Assert.Equal(1, r.Total));

        // As dependências continuam visíveis, mas à parte.
        Assert.Equal(2, progress.Reported[^1].Dependencies);
    }

    private static ModpackVersion NewDraftVersion() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0", LoaderVersion = "21.1.234" };

    private static ModIngestionItem Item(string projectId) =>
        new(ModFileOrigin.Modrinth, projectId, null, FileSide.Both);

    private static ModDependency Req(string id) => new(id, ModDependencyKind.Required);

    private static ModDependency Opt(string id) => new(id, ModDependencyKind.Optional);

    // ---- Fakes ----

    private sealed class FakeResolver(
        Dictionary<string, IReadOnlyList<ModDependency>> deps,
        HashSet<string>? notFound = null) : IModResolver
    {
        private readonly HashSet<string> _notFound = notFound ?? [];

        /// <summary>Lado que a origem declara, quando declara.</summary>
        public Dictionary<string, FileSide> SideByProject { get; } = [];

        public ModFileOrigin Origin => ModFileOrigin.Modrinth;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
        {
            if (_notFound.Contains(request.ProjectId))
                return Task.FromResult<ModResolution>(new ModResolution.NotFound("não encontrado"));

            var d = deps.TryGetValue(request.ProjectId, out var found) ? found : [];
            var resolved = new ModResolution.Resolved(
                $"{request.ProjectId}-v1",
                $"{request.ProjectId}.jar",
                null,
                10,
                new Uri($"https://example.test/{request.ProjectId}.jar"),
                d,
                Side: SideByProject.TryGetValue(request.ProjectId, out var side) ? side : null);

            return Task.FromResult<ModResolution>(resolved);
        }
    }

    /// <summary>Estoura de um jeito que ninguém previu — o caso que travava tudo.</summary>
    private sealed class ExplodeResolver : IModResolver
    {
        public ModFileOrigin Origin => ModFileOrigin.Modrinth;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("a API devolveu algo inesperado");
    }

    private sealed class FakeDownloader : IModDownloader
    {
        public Task<Stream> OpenAsync(Uri url, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[10]));
    }

    private sealed class FakeBlobStore : FakeBlobStoreBase
    {
        public override Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(false);

        // Sha fixo: os arquivos só diferem por ProjectSlug/Path, então o
        // conteúdo idêntico não atrapalha a dedup (que também olha o slug).
        public override Task<string>
            PutAsync(Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('a', 64));

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[10]));

        public override Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult<Uri?>(null);

    }

    private sealed class FakeModpackRepository : FakeModpackRepositoryBase
    {
        public ModpackVersion? Version { get; init; }

        /// <summary>Quantas vezes a versão foi gravada — usado para travar a ordem save→aviso.</summary>
        public int SaveCount { get; private set; }

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) => Task.FromResult(Version);

        public override Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public override Task SaveVersionStateAsync(ModpackVersion version, CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        /// <summary>Lotes descarregados pela ingestão, na ordem.</summary>
        public List<int> Lotes { get; } = [];

        public override Task AddFilesAsync(Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct)
        {
            Lotes.Add(files.Count);
            return Task.CompletedTask;
        }

        public override Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) => Task.CompletedTask;

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            // O caso de uso lê MinecraftVersion/Loader do modpack agora.
            return Task.FromResult<Modpack?>(new Modpack
            {
                Slug = "test", Name = "Test", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
            });
        }

    }
}
