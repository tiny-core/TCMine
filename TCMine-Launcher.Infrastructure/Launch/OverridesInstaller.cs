using System.IO.Compression;
using System.Net;
using TCMine_Domain.Launcher;
using TCMine_Launcher.Infrastructure.Configuration;
using TCMine_Launcher.Infrastructure.FileSystem;
using TCMine_Launcher.Infrastructure.Networking;

namespace TCMine_Launcher.Infrastructure.Launch;

/// <summary>
///     Aplica o bundle de overrides do modpack na pasta do jogo, gated por versão; numa atualização preserva
///     os arquivos do jogador (snapshot/restore via <see cref="PlayerDataProfile" />). Colaborador interno
///     do <see cref="LaunchOrchestrator" />.
/// </summary>
internal sealed class OverridesInstaller(ServerConfig config)
{
    private readonly HttpClient _http = HttpClientProvider.Shared;

    public async Task EnsureAsync(InstalledModpack instance, CancellationToken ct = default)
    {
        if (!instance.HasOverrides) return;
        if (instance.OverridesVersion == instance.ManifestVersion) return;

        var url = config.Resolve($"/api/modpacks/{instance.ModpackId}/overrides.zip");
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            instance.OverridesVersion = instance.ManifestVersion;
            return;
        }

        resp.EnsureSuccessStatusCode();

        var gameDir = LauncherPaths.InstanceGameDir(instance.ModpackId);
        Directory.CreateDirectory(gameDir);

        using var buffer = new MemoryStream();
        await using (var net = await resp.Content.ReadAsStreamAsync(ct))
        {
            await net.CopyToAsync(buffer, ct);
        }

        buffer.Position = 0;

        var snapshotDir = instance.OverridesVersion is not null ? SnapshotPlayerData(gameDir) : null;
        try
        {
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            zip.ExtractToDirectory(gameDir, true);

            if (snapshotDir is not null) RestorePlayerData(snapshotDir, gameDir);
        }
        finally
        {
            if (snapshotDir is not null) TryDeleteDir(snapshotDir);
        }

        instance.OverridesVersion = instance.ManifestVersion;
    }

    private static string SnapshotPlayerData(string gameDir)
    {
        var temp = Path.Combine(Path.GetTempPath(), "tcmine-cfg-" + Guid.NewGuid().ToString("N"));
        foreach (var rel in PlayerDataProfile.EnumerateExisting(gameDir))
        {
            var src = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
            var dst = Path.Combine(temp, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
        }

        return temp;
    }

    private static void RestorePlayerData(string snapshotDir, string gameDir)
    {
        if (!Directory.Exists(snapshotDir)) return;
        foreach (var src in Directory.EnumerateFiles(snapshotDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(snapshotDir, src);
            var dst = Path.Combine(gameDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
        }
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch
        {
            /* noop */
        }
    }
}