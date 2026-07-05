using System.Net.Http;
using TCMine_Application.Contracts;
using TCMine_Domain.Launcher;
using TCMine_Launcher.Infrastructure.FileSystem;
using TCMine_Launcher.Infrastructure.Networking;

namespace TCMine_Launcher.Infrastructure.Launch;

/// <summary>
/// Baixa os mods do manifesto para uma cache partilhada e copia para a instância. Os jars são servidos
/// pelo próprio TCMine Server (URLs já apontam para <c>/files/...</c>) — integridade por existência
/// (o <c>ModDto</c> não traz Sha1). Colaborador interno do <see cref="LaunchOrchestrator"/>.
/// </summary>
internal sealed class ModInstaller
{
    private const int MaxParallel = 4;
    private readonly HttpClient _http = HttpClientProvider.Shared;

    public async Task EnsureModsAsync(
        string modpackId, IReadOnlyList<ModDto> mods, IProgress<LaunchProgress> progress,
        CancellationToken ct, bool prune)
    {
        var gameDir = LauncherPaths.InstanceGameDir(modpackId);
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        if (prune) PruneUnlisted(mods, modsDir);

        var pending = mods.Where(m => !string.IsNullOrEmpty(m.DownloadUrl)).ToList();
        if (pending.Count == 0) return;

        var total = pending.Count;
        var completed = 0;
        using var gate = new SemaphoreSlim(MaxParallel);
        Directory.CreateDirectory(LauncherPaths.ModCacheDir);

        var tasks = pending.Select(async mod =>
        {
            await gate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                var dest = Path.Combine(gameDir, FolderFor(mod.Target), mod.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                if (!File.Exists(dest))
                {
                    var cached = Path.Combine(LauncherPaths.ModCacheDir, mod.FileName);
                    if (!File.Exists(cached)) await DownloadAsync(mod.DownloadUrl, cached, ct);
                    File.Copy(cached, dest, true);
                }

                var done = Interlocked.Increment(ref completed);
                progress.Report(new LaunchProgress(
                    LaunchState.DownloadingAssets, (double)done / total * 100,
                    $"A descarregar mods ({done}/{total})"));
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task DownloadAsync(string url, string dest, CancellationToken ct)
    {
        var tmp = dest + ".part";
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            await using (var net = await resp.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(tmp))
            {
                await net.CopyToAsync(file, ct);
            }

            File.Move(tmp, dest, true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    private static string FolderFor(string? target) => target?.ToLowerInvariant() switch
    {
        "resourcepack" => "resourcepacks",
        "shaderpack" => "shaderpacks",
        _ => "mods"
    };

    private static void PruneUnlisted(IReadOnlyList<ModDto> mods, string modsDir)
    {
        var wanted = mods.Select(m => m.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var jar in Directory.EnumerateFiles(modsDir, "*.jar"))
            if (!wanted.Contains(Path.GetFileName(jar)))
                TryDelete(jar);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* noop */ }
    }
}
