using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using TCMine_Application.Contracts;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.Infrastructure;

/// <summary>
/// Sync <b>incremental</b> das configs player-owned (keybinds/opções, shaders, minimapa — incluindo o
/// cache de mapa dos servidores; ver <see cref="PlayerDataProfile"/>) com o servidor, por
/// <c>(uuid, modpackId)</c>. Compara manifestos (caminho → hash) e transfere <b>só o que mudou</b>, em vez
/// do conjunto inteiro — poupa rede. No prepare puxa; ao fechar o jogo empurra. Colaborador interno do
/// <see cref="LaunchOrchestrator"/>. Best-effort: falhas de rede não podem partir o launch.
/// </summary>
internal sealed class PlayerConfigSync(ServerConfig config)
{
    // Entrada do manifesto dentro do zip de push; igual ao nome que o servidor guarda em disco.
    private const string ManifestEntry = ".tcmine-manifest.json";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient _http = HttpClientProvider.Shared;

    /// <summary>
    /// Servidor → local. Baixa só os ficheiros cujo hash difere (ou faltam) localmente. Salta se o
    /// manifesto remoto já for o aplicado (<see cref="InstalledModpack.ConfigSyncedAt"/>). Sem manifesto
    /// no servidor (404) = nada a puxar.
    /// </summary>
    public async Task PullAsync(
        InstalledModpack instance, string uuid, Action<string>? report = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return;

        using var resp = await _http.GetAsync(ManifestUrl(uuid, instance.ModpackId), ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return;
        resp.EnsureSuccessStatusCode();

        var server = await resp.Content.ReadFromJsonAsync<PlayerConfigManifest>(Json, ct);
        if (server is null) return;
        // Já temos exatamente esta versão aplicada → nem calcula o diff.
        if (instance.ConfigSyncedAt == server.UpdatedAt) return;

        var gameDir = LauncherPaths.InstanceGameDir(instance.ModpackId);
        Directory.CreateDirectory(gameDir);

        var local = await BuildManifestAsync(gameDir, ct);
        var need = server.Files
            .Where(kv => !local.Files.TryGetValue(kv.Key, out var li) || li.Hash != kv.Value.Hash)
            .Select(kv => kv.Key)
            .ToList();

        if (need.Count > 0)
        {
            report?.Invoke($"A baixar configurações do jogador ({need.Count} ficheiros)…");
            using var bundleResp = await _http.PostAsJsonAsync(
                BundleUrl(uuid, instance.ModpackId), new PlayerConfigBundleRequest(need), Json, ct);
            bundleResp.EnsureSuccessStatusCode();

            var tmp = TempZip();
            try
            {
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var net = await bundleResp.Content.ReadAsStreamAsync(ct))
                {
                    await net.CopyToAsync(fs, ct);
                }

                ExtractInto(tmp, gameDir);
            }
            finally
            {
                TryDelete(tmp);
            }
        }

        instance.ConfigSyncedAt = server.UpdatedAt;
    }

    /// <summary>
    /// Local → servidor. Envia só os ficheiros novos/alterados + o manifesto completo (para o servidor
    /// reconciliar remoções). Nada mudou → no-op. Atualiza <see cref="InstalledModpack.ConfigSyncedAt"/>.
    /// </summary>
    public async Task PushAsync(
        InstalledModpack instance, string uuid, string accessToken, Action<string>? report = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(accessToken)) return;

        var gameDir = LauncherPaths.InstanceGameDir(instance.ModpackId);
        var local = await BuildManifestAsync(gameDir, ct);

        // Manifesto atual do servidor (404 = servidor vazio).
        var serverFiles = new Dictionary<string, PlayerConfigFileInfo>();
        using (var mResp = await _http.GetAsync(ManifestUrl(uuid, instance.ModpackId), ct))
            if (mResp.IsSuccessStatusCode &&
                await mResp.Content.ReadFromJsonAsync<PlayerConfigManifest>(Json, ct) is { } server)
                serverFiles = server.Files;

        var toUpload = local.Files
            .Where(kv => !serverFiles.TryGetValue(kv.Key, out var si) || si.Hash != kv.Value.Hash)
            .Select(kv => kv.Key)
            .ToList();
        var hasDeletions = serverFiles.Keys.Any(k => !local.Files.ContainsKey(k));

        // Nada novo/alterado e nada a remover → não há o que sincronizar.
        if (toUpload.Count == 0 && !hasDeletions) return;

        report?.Invoke(toUpload.Count > 0
            ? $"A enviar configurações do jogador ({toUpload.Count} ficheiros)…"
            : "A sincronizar configurações do jogador…");

        var tmp = TempZip();
        try
        {
            BuildPushZip(gameDir, toUpload, local, tmp);

            await using var fs = new FileStream(tmp, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var content = new StreamContent(fs);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            using var req = new HttpRequestMessage(HttpMethod.Put, PushUrl(uuid, instance.ModpackId))
            {
                Content = content
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            await using var body = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("updatedAt", out var prop) &&
                prop.TryGetDateTimeOffset(out var updatedAt))
                instance.ConfigSyncedAt = updatedAt;
        }
        finally
        {
            TryDelete(tmp);
        }
    }

    /// <summary>Manifesto dos ficheiros player-owned existentes em <paramref name="gameDir"/> (hash SHA-256).</summary>
    private static async Task<PlayerConfigManifest> BuildManifestAsync(string gameDir, CancellationToken ct)
    {
        var files = new Dictionary<string, PlayerConfigFileInfo>();
        foreach (var rel in PlayerDataProfile.EnumerateExisting(gameDir))
        {
            var full = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) continue;

            await using var fs = File.OpenRead(full);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
            files[rel] = new PlayerConfigFileInfo(hash, fs.Length);
        }

        return new PlayerConfigManifest(DateTimeOffset.UtcNow, files);
    }

    /// <summary>Zip do push: os ficheiros a enviar + o manifesto completo (entrada especial).</summary>
    private static void BuildPushZip(
        string gameDir, IReadOnlyList<string> toUpload, PlayerConfigManifest manifest, string zipPath)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var rel in toUpload)
        {
            var full = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) zip.CreateEntryFromFile(full, rel, CompressionLevel.Optimal);
        }

        var entry = zip.CreateEntry(ManifestEntry);
        using var es = entry.Open();
        JsonSerializer.Serialize(es, manifest, Json);
    }

    /// <summary>Extrai o zip na pasta do jogo, sobrescrevendo. Ignora entradas fora da pasta (zip-slip).</summary>
    private static void ExtractInto(string zipPath, string gameDir)
    {
        var root = Path.GetFullPath(gameDir);
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // entrada de diretório
            var dest = Path.GetFullPath(Path.Combine(gameDir, entry.FullName));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, true);
        }
    }

    private Uri ManifestUrl(string uuid, string modpackId) =>
        config.Resolve($"/players/{uuid}/configs/{modpackId}/manifest");

    private Uri BundleUrl(string uuid, string modpackId) =>
        config.Resolve($"/players/{uuid}/configs/{modpackId}/bundle");

    private Uri PushUrl(string uuid, string modpackId) =>
        config.Resolve($"/players/{uuid}/configs/{modpackId}/push");

    private static string TempZip() =>
        Path.Combine(Path.GetTempPath(), "tcmine-cfg-" + Guid.NewGuid().ToString("N") + ".zip");

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort */ }
    }
}
