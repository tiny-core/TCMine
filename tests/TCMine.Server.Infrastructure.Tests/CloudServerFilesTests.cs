using System.Text.Json;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Infrastructure.Instances;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>O arquivo que o mod lê: formato, permissão e limpeza.</summary>
public sealed class CloudServerFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tccloud-files-{Guid.CreateVersion7():N}");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grava_url_e_chave_e_apaga_sem_erro()
    {
        var files = new CloudServerFiles(new PastaFixa(_root));
        var servidor = Guid.CreateVersion7();

        await files.WriteAsync(servidor, new Uri("https://tcmine.exemplo.com/"), "tcs_abcdefghijkm_segredo", Ct);

        var caminho = Path.Combine(_root, servidor.ToString(), CloudServerFiles.FileName);
        using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(caminho, Ct)))
        {
            json.RootElement.GetProperty("url").GetString().ShouldBe("https://tcmine.exemplo.com");
            json.RootElement.GetProperty("key").GetString().ShouldBe("tcs_abcdefghijkm_segredo");
        }

        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(caminho).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Exists(caminho + ".tmp").ShouldBeFalse();

        await files.DeleteAsync(servidor, Ct);
        File.Exists(caminho).ShouldBeFalse();
        await files.DeleteAsync(servidor, Ct); // idempotente
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class PastaFixa(string root) : IInstanceMaterializer
    {
        public Task MaterializeAsync(Guid gameServerId, ModpackVersion version, CancellationToken ct) =>
            throw new NotSupportedException();

        public string GetInstancePath(Guid gameServerId) => Path.Combine(root, gameServerId.ToString());

        public Task DeleteInstanceAsync(Guid gameServerId, CancellationToken ct) => throw new NotSupportedException();
    }
}
