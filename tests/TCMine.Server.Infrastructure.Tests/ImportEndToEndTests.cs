using TCMine.Contracts.Servers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Importar um pack grande e resolvê-lo, do começo ao fim, contra um
///     PostgreSQL de verdade.
///     Existe porque este fluxo queimou três releases seguidas, e cada falha só
///     apareceu em produção: uma coluna curta demais para o identificador de um
///     override, o registro da origem estourando um <c>varchar(512)</c> num pack
///     de centenas de mods, e uma pendência trocada de motivo colidindo com ela
///     mesma no índice único.
///     Nenhuma delas era detectável em SQLite — ele aceita qualquer texto num
///     varchar e ignora o limite declarado. Por isso este teste roda no banco de
///     produção, com volume parecido com o real, e não sobre fakes.
///     Sem <c>TCMINE_TEST_POSTGRES</c> ele se pula; no CI a variável está
///     definida.
/// </summary>
public sealed class ImportEndToEndTests
{
    /// <summary>Perto do All the Mods 10 (481), que é o pack que quebrou tudo.</summary>
    private const int Mods = 300;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pack_grande_importa_resolve_e_registra_o_que_faltou()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        var repo = new ModpackRepository(new Fabrica(postgres));
        var blobs = new BlobsEmMemoria();

        var pack = PackGrande();
        var fila = new FilaQueGuarda();

        var import = new ImportUpstreamPack(
            [new OrigemFixa(pack)], repo, blobs,
            new IngestionScheduler(repo, fila),
            new ProgressoMudo(), new DownloaderFixo(), new EscopoDeTeste());

        var criado = await import.HandleAsync(ModFileOrigin.CurseForge, "925200", null, Ct);

        criado.Succeeded.ShouldBeTrue(criado.Error);

        // ---- O que a importação gravou ----

        var modpack = await repo.GetByIdAsync(criado.Value, Ct);
        var versao = (await repo.ListVersionsAsync(criado.Value, Ct)).Single();

        modpack.ShouldNotBeNull();
        versao.State.ShouldBe(ModpackVersionState.Draft);

        // O snapshot da origem guarda um par projeto/arquivo e o NOME de cada
        // mod: num pack deste tamanho são dezenas de KB, e a coluna já foi
        // varchar(512).
        var comSnapshot = await repo.GetVersionAsync(versao.Id, Ct);
        comSnapshot!.UpstreamSnapshotJson.ShouldNotBeNullOrEmpty();
        comSnapshot.UpstreamSnapshotJson!.Length.ShouldBeGreaterThan(10_000);

        // Overrides com caminho no limite: é o identificador deles (caminho MAIS
        // um prefixo) que estourava a coluna.
        comSnapshot.Files.ShouldContain(f => f.Path.Length >= ModpackFile.MaxPathLength - 10);

        // ---- A ingestão, sobre o que a importação enfileirou ----

        fila.Itens.Count.ShouldBe(Mods);

        var ingestao = new ModpackIngestionService(
            repo, blobs, [new ResolverDeTeste()], new DownloaderFixo(),
            new InspetorMudo(), new ProgressoMudo(),
            NullLogger<ModpackIngestionService>.Instance);

        await ingestao.IngestAsync(versao.Id, fila.Itens, Ct);

        var depois = await repo.GetVersionAsync(versao.Id, Ct);

        // Volta ao rascunho: pendência não reprova a versão.
        depois!.State.ShouldBe(ModpackVersionState.Draft);

        // Um a cada dez é recusado pelo autor e um a cada dez não tem arquivo —
        // a proporção não importa, o que importa é que os dois caminhos rodem
        // sobre o banco real, com o índice único de pendência valendo.
        depois.ManualUploads.ShouldNotBeEmpty();

        depois.ManualUploads.ShouldContain(p => p.Reason == PendingModReason.DistributionDenied);
        depois.ManualUploads.ShouldContain(p => p.Reason == PendingModReason.NoCompatibleFile);

        // Nenhuma pendência Queued sobreviveu: ou virou arquivo, ou virou motivo
        // real. Uma sobrando significaria que a substituição por slug falhou.
        depois.PendingMods.ShouldNotContain(p => p.Reason == PendingModReason.Queued);

        // E os mods que vieram, vieram.
        depois.Files.Count(f => f.Origin == ModFileOrigin.CurseForge).ShouldBeGreaterThan(200);
    }

    [Fact]
    public async Task Reingerir_nao_duplica_nem_baixa_de_novo()
    {
        // A segunda passada é onde moravam dois bugs: pendência trocada de
        // motivo colidindo no índice único, e o mesmo .jar descendo outra vez
        // para ser descartado depois de hasheado.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);

        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        var repo = new ModpackRepository(new Fabrica(postgres));
        var blobs = new BlobsEmMemoria();
        var downloader = new DownloaderFixo();
        var fila = new FilaQueGuarda();

        var import = new ImportUpstreamPack(
            [new OrigemFixa(PackGrande())], repo, blobs,
            new IngestionScheduler(repo, fila),
            new ProgressoMudo(), new DownloaderFixo(), new EscopoDeTeste());

        var criado = await import.HandleAsync(ModFileOrigin.CurseForge, "925200", null, Ct);
        var versao = (await repo.ListVersionsAsync(criado.Value, Ct)).Single();

        ModpackIngestionService Servico() => new(
            repo, blobs, [new ResolverDeTeste()], downloader,
            new InspetorMudo(), new ProgressoMudo(),
            NullLogger<ModpackIngestionService>.Instance);

        await Servico().IngestAsync(versao.Id, fila.Itens, Ct);

        var primeiraPassada = downloader.Baixados;
        var apos = await repo.GetVersionAsync(versao.Id, Ct);
        var arquivosDepoisDaPrimeira = apos!.Files.Count;

        // Volta a Draft entre as duas: a ingestão exige rascunho.
        await Should.NotThrowAsync(() => Servico().IngestAsync(versao.Id, fila.Itens, Ct));

        var final = await repo.GetVersionAsync(versao.Id, Ct);

        final!.Files.Count.ShouldBe(arquivosDepoisDaPrimeira, "reingerir não pode acumular arquivo");
        downloader.Baixados.ShouldBe(primeiraPassada, "o que não mudou não desce de novo");
    }

    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    /// <summary>
    ///     Um pack com o formato do que quebra: centenas de mods e um override
    ///     com o caminho no tamanho máximo que o domínio aceita.
    /// </summary>
    private static UpstreamPack PackGrande()
    {
        var mods = Enumerable.Range(0, Mods)
            .Select(i => new UpstreamPackMod(
                $"{100000 + i}", $"{900000 + i}", true, $"Mod de Exemplo Número {i}"))
            .ToList();

        var caminhoLongo = "config/" + new string('x', ModpackFile.MaxPathLength - "config/".Length - 5) + ".toml";

        return new UpstreamPack
        {
            ProjectId = "925200",
            FileId = "1",
            VersionLabel = "8.0",
            Name = "All the Mods 10",
            Author = "ATMTeam",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge,
            LoaderVersion = "21.1.247",
            Mods = mods,
            Overrides =
            [
                new UpstreamPackOverride(caminhoLongo, "conteudo"u8.ToArray()),
                new UpstreamPackOverride("config/normal.toml", "conteudo"u8.ToArray())
            ]
        };
    }

    // ---- Fakes ----

    private sealed class Fabrica(PostgresTestDatabase db) : IDbContextFactory<TcMineDbContext>
    {
        public TcMineDbContext CreateDbContext() => db.CreateContext();
    }

    private sealed class OrigemFixa(UpstreamPack pack) : IUpstreamPackSource
    {
        public ModFileOrigin Origin => ModFileOrigin.CurseForge;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct) =>
            Task.FromResult<UpstreamPack?>(pack);

        public Task<IReadOnlyList<UpstreamPackSummary>> SearchPacksAsync(
            string text, int limit, CancellationToken ct) => throw new NotSupportedException();

        public Task<UpstreamRelease?> GetLatestReleaseAsync(string projectId, CancellationToken ct) =>
            Task.FromResult<UpstreamRelease?>(null);

        public Task<IReadOnlyDictionary<string, string>> GetFileNamesAsync(
            IReadOnlyList<string> fileIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));

        public Task<UpstreamServerPack?> GetServerPackAsync(
            string projectId, string fileId, CancellationToken ct) =>
            Task.FromResult<UpstreamServerPack?>(null);

        public Task<IServerPackReader?> OpenServerPackAsync(
            string projectId, string serverPackFileId, CancellationToken ct) =>
            Task.FromResult<IServerPackReader?>(null);
    }

    /// <summary>
    ///     Resolve a maioria, recusa uma parte por decisão do autor e não acha
    ///     outra — os três desfechos que a ingestão precisa saber tratar.
    /// </summary>
    private sealed class ResolverDeTeste : IModResolver
    {
        public ModFileOrigin Origin => ModFileOrigin.CurseForge;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
        {
            var n = int.Parse(request.ProjectId, System.Globalization.CultureInfo.InvariantCulture);

            ModResolution resolucao = (n % 10) switch
            {
                0 => new ModResolution.DistributionDenied(
                    $"Mod {n}", new Uri($"https://exemplo/{n}")),
                1 => new ModResolution.NotFound($"Sem arquivo para 1.21.1 do projeto {n}."),
                _ => new ModResolution.Resolved(
                    $"{n}", $"mod-{n}.jar", null, 1024,
                    new Uri($"https://exemplo/{n}.jar"), [])
            };

            return Task.FromResult(resolucao);
        }
    }

    private sealed class BlobsEmMemoria : IBlobStore
    {
        private readonly Dictionary<string, byte[]> _conteudo = [];

        public Task<bool> ExistsAsync(string sha256, CancellationToken ct) =>
            Task.FromResult(_conteudo.ContainsKey(sha256));

        public async Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);

            var bytes = buffer.ToArray();
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            _conteudo[sha] = bytes;
            return sha;
        }

        public Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(_conteudo[sha256]));

        public Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult<Uri?>(null);

        public Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<string?>(null);
    }

    private sealed class DownloaderFixo : IModDownloader
    {
        public int Baixados { get; private set; }

        public Task<Stream> OpenAsync(Uri url, CancellationToken ct)
        {
            Baixados++;

            // Conteúdo diferente por URL: com bytes iguais o blob store
            // deduplicaria tudo num hash só e o teste não veria os arquivos.
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(url.ToString())));
        }
    }

    private sealed class FilaQueGuarda : IIngestionQueue
    {
        public List<ModIngestionItem> Itens { get; } = [];

        public ValueTask EnqueueAsync(
            Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
        {
            Itens.AddRange(items);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InspetorMudo : IModJarInspector
    {
        public Task<ModJarInfo?> InspectAsync(Stream jar, CancellationToken ct) =>
            Task.FromResult<ModJarInfo?>(null);
    }

    private sealed class ProgressoMudo : IJobProgressReporter
    {
        public void Report(Guid scopeId, JobProgress progress) { }
        public void Complete(Guid scopeId, string? error = null) { }
        public bool IsRunning(Guid scopeId) => false;
    }

    private sealed class EscopoDeTeste : ICurrentUserScope
    {
        public Guid OwnerId { get; } = Guid.CreateVersion7();
        public Guid? UserId => OwnerId;
        public bool IsInstanceAdmin => true;

        public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult<ServerRoleDto?>(ServerRoleDto.Owner);
    }
}
