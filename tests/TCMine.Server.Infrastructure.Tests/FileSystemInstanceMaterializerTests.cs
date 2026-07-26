using Microsoft.Extensions.Options;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Infrastructure.Instances;

namespace TCMine.Server.Infrastructure.Tests;

public sealed class FileSystemInstanceMaterializerTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "tcmine-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true);
    }

    [Fact]
    public async Task Materializa_lado_servidor_e_ignora_client_only()
    {
        var (materializer, blobs) = Build();
        var version = NewVersion(
            Mod("mods/jei.jar", FileSide.Both, blobs.Put("conteudo-jei")),
            Mod("mods/srv.jar", FileSide.ServerOnly, blobs.Put("conteudo-srv")),
            Mod("mods/shader.jar", FileSide.ClientOnly, blobs.Put("conteudo-shader")),
            Override("config/mod.toml", blobs.Put("a=1")));

        var serverId = Guid.CreateVersion7();
        await materializer.MaterializeAsync(serverId, version, CancellationToken.None);

        var root = materializer.GetInstancePath(serverId);
        Assert.True(File.Exists(Path.Combine(root, "mods/jei.jar")));
        Assert.True(File.Exists(Path.Combine(root, "mods/srv.jar")));
        Assert.True(File.Exists(Path.Combine(root, "config/mod.toml")));
        Assert.False(File.Exists(Path.Combine(root, "mods/shader.jar"))); // ClientOnly fora
    }

    [Fact]
    public async Task Rematerializar_remove_mod_que_saiu_e_preserva_o_mundo()
    {
        var (materializer, blobs) = Build();
        var serverId = Guid.CreateVersion7();

        // v1: jei + sodium
        await materializer.MaterializeAsync(serverId,
            NewVersion(Mod("mods/jei.jar", FileSide.Both, blobs.Put("jei")),
                Mod("mods/sodium.jar", FileSide.Both, blobs.Put("sodium"))),
            CancellationToken.None);

        // O servidor gerou um mundo entre um boot e outro.
        var root = materializer.GetInstancePath(serverId);
        Directory.CreateDirectory(Path.Combine(root, "world"));
        await File.WriteAllTextAsync(Path.Combine(root, "world/level.dat"), "mundo");

        // v2: só jei (sodium saiu)
        await materializer.MaterializeAsync(serverId,
            NewVersion(Mod("mods/jei.jar", FileSide.Both, blobs.Put("jei"))),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(root, "mods/jei.jar")));
        Assert.False(File.Exists(Path.Combine(root, "mods/sodium.jar"))); // removido
        Assert.True(File.Exists(Path.Combine(root, "world/level.dat"))); // mundo preservado
        Assert.Equal("mundo", await File.ReadAllTextAsync(Path.Combine(root, "world/level.dat")));
    }

    private (FileSystemInstanceMaterializer, FakeBlobStore) Build()
    {
        var blobs = new FakeBlobStore(Path.Combine(_tmp, "blobs"));
        var opts = Options.Create(new InstanceOptions { RootPath = Path.Combine(_tmp, "instances") });
        return (new FileSystemInstanceMaterializer(blobs, opts), blobs);
    }

    private static ModpackVersion NewVersion(params ModpackFile[] files)
    {
        var v = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(),
            Version = "1.0",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge,
            LoaderVersion = "21.1.234"
        };
        foreach (var f in files) v.UpsertFile(f);
        return v;
    }

    private static ModpackFile Mod(string path, FileSide side, string sha)
    {
        return new ModpackFile
        {
            ModpackVersionId = Guid.Empty, ProjectSlug = path, Path = path,
            Sha256 = sha, SizeBytes = 10, Side = side, Origin = ModFileOrigin.Modrinth
        };
    }

    private static ModpackFile Override(string path, string sha)
    {
        return new ModpackFile
        {
            ModpackVersionId = Guid.Empty, Path = path,
            Sha256 = sha, SizeBytes = 10, Side = FileSide.Both, Origin = ModFileOrigin.Override
        };
    }

    // Blob store fake: guarda conteúdo por um "sha" arbitrário (o nome do ficheiro).
    private sealed class FakeBlobStore(string root) : IBlobStore
    {
        private int _n;

        public Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct)
        {
            var p = Path.Combine(root, sha256);
            return Task.FromResult<string?>(File.Exists(p) ? p : null);
        }

        public Task<Stream> OpenAsync(string sha256, CancellationToken ct)
        {
            return Task.FromResult<Stream>(File.OpenRead(Path.Combine(root, sha256)));
        }

        public Task<bool> ExistsAsync(string sha256, CancellationToken ct)
        {
            return Task.FromResult(File.Exists(Path.Combine(root, sha256)));
        }

        public Task<string> PutAsync(Stream c, string? e, string ct2, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Uri?> TryGetDirectUrlAsync(string s, TimeSpan l, CancellationToken ct)
        {
            return Task.FromResult<Uri?>(null);
        }

        public string Put(string content)
        {
            Directory.CreateDirectory(root);
            var sha = $"fake{_n++:D2}";
            File.WriteAllText(Path.Combine(root, sha), content);
            return sha;
        }
    }
}