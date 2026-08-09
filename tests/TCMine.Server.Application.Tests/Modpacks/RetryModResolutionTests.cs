using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

using TCMine.Server.Application.Tests.Fakes;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class RetryModResolutionTests
{
    [Fact]
    public async Task Reenfileira_apenas_os_mods_que_faltaram()
    {
        // Metade do pack baixou antes da falha: o que já está no disco foi
        // conferido por hash e continua válido — rebaixar tudo seria desperdício.
        var (repo, queue, version) = Cenario(baixados: ["1", "2"]);

        var result = await new RetryModResolution(repo, queue).HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value);
        Assert.Equal(["3", "4"], queue.Enfileirados.Select(i => i.ProjectId).Order());
        Assert.Equal(ModpackVersionState.Draft, version.State);
        Assert.Null(version.FailureReason);
    }

    [Fact]
    public async Task Nao_enfileira_nada_quando_tudo_ja_estava_baixado()
    {
        var (repo, queue, version) = Cenario(baixados: ["1", "2", "3", "4"]);

        var result = await new RetryModResolution(repo, queue).HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Value);
        Assert.Empty(queue.Enfileirados);
    }

    [Fact]
    public async Task Recusa_reparar_versao_publicada()
    {
        var (repo, queue, version) = Cenario(baixados: ["1", "2", "3", "4"], falhou: false);
        version.MarkResolving();
        version.MarkReady();

        var result = await new RetryModResolution(repo, queue).HandleAsync(version.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(queue.Enfileirados);
    }

    [Fact]
    public async Task Reenfileira_pendencias_mas_pula_as_de_redistribuicao_negada()
    {
        var (repo, queue, version) = Cenario(baixados: ["1", "2", "3", "4"], falhou: false);

        version.UpsertPending(new PendingMod
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "9",
            DisplayName = "Mod fora do ar",
            Origin = ModFileOrigin.CurseForge,
            Reason = PendingModReason.Transient
        });

        version.UpsertPending(new PendingMod
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "10",
            DisplayName = "Mod bloqueado pelo autor",
            Origin = ModFileOrigin.CurseForge,
            Reason = PendingModReason.DistributionDenied
        });

        var result = await new RetryModResolution(repo, queue).HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);

        // O bloqueado pelo autor nunca vai resolver: tentar de novo só gasta cota.
        var item = Assert.Single(queue.Enfileirados);
        Assert.Equal("9", item.ProjectId);
    }

    // ---- Fixtures ----

    private static (FakeRepo Repo, FakeQueue Queue, ModpackVersion Version) Cenario(
        string[] baixados, bool falhou = true)
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

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = "1.0.0",
            LoaderVersion = "21.1.100",
            UpstreamSnapshotJson = new UpstreamSnapshot
            {
                Mods = new Dictionary<string, string>
                {
                    ["1"] = "111", ["2"] = "222", ["3"] = "333", ["4"] = "444"
                },
                Overrides = new Dictionary<string, string>()
            }.ToJson()
        };

        foreach (var projectId in baixados)
        {
            version.UpsertFile(new ModpackFile
            {
                ModpackVersionId = version.Id,
                ProjectSlug = projectId,
                Path = $"mods/{projectId}.jar",
                Sha256 = new string('a', 64),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.CurseForge
            });
        }

        if (falhou)
            version.MarkFailed("timeout na API");

        return (new FakeRepo(modpack, version), new FakeQueue(), version);
    }

    // ---- Fakes ----

    private sealed class FakeQueue : IIngestionQueue
    {
        public List<ModIngestionItem> Enfileirados { get; } = [];

        public ValueTask EnqueueAsync(Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
        {
            Enfileirados.AddRange(items);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Devolve a MESMA instância, para as transições ficarem visíveis ao teste.</summary>
    private sealed class FakeRepo(Modpack modpack, ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(modpack);

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

    }
}
