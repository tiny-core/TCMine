using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Minecraft;

namespace TCMine_Server.Endpoints;

/// <summary>
/// Sync <b>incremental</b> das configs do jogador entre PCs, por <c>(uuid, modpackId)</c>. Os ficheiros
/// player-owned (keybinds/opções, shaders, minimapa — incluindo o cache de mapa dos <b>servidores</b>)
/// vivem <b>descompactados em disco</b> (<c>tcmine-data/player-configs/{uuid}/{modpackId}/</c>) ao lado de
/// um <c>.tcmine-manifest.json</c> (caminho → hash+tamanho). Só os ficheiros que mudaram trafegam — nunca
/// o conjunto inteiro — para não sobrecarregar a rede.
///
/// - <b>GET /manifest</b> (aberto): o manifesto atual, para o cliente diferenciar.
/// - <b>POST /bundle</b> (aberto): zip só com os caminhos pedidos (o cliente baixa o que lhe falta).
/// - <b>PUT /push</b> (auth): zip só com os ficheiros novos/alterados + o novo manifesto; o servidor
///   aplica-os e apaga os que saíram do manifesto. Exige token Minecraft do UUID (ver
///   <see cref="MinecraftAuthService"/>). Tudo rate-limited pela política "configs".
/// </summary>
public static class PlayerConfigEndpoints
{
    // Manifesto guardado ao lado dos ficheiros; nunca é servido como ficheiro de config normal.
    private const string ManifestFile = ".tcmine-manifest.json";

    // Teto defensivo do corpo do PUT (o diff pode incluir o cache de mapa na 1ª vez). Ajustar se preciso.
    private const long MaxConfigBytes = 256L * 1024 * 1024; // 256 MB

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapPlayerConfigEndpoints(this IEndpointRouteBuilder app)
    {
        // Cota de disco por conjunto (uuid, modpackId): teto do tamanho total declarado no manifesto de um
        // push. Barra o abuso de encher o servidor mesmo com um token válido (ou durante o fail-open da
        // validação Mojang). Configurável em PlayerConfigs:MaxSetMb; default 1 GB (cobre cache de mapa).
        var maxSetBytes =
            (app.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<long?>("PlayerConfigs:MaxSetMb")
             ?? 1024) * 1024 * 1024;

        // Manifesto atual. Leitura AUTENTICADA: exige o token Minecraft do próprio UUID — sem isso, qualquer
        // um que saiba um (uuid, modpackId) baixaria as configs alheias (UUID é público). Ver AuthorizeAsync.
        app.MapGet("/players/{uuid}/configs/{modpackId}/manifest", async (
            string uuid, string modpackId, HttpContext ctx, IHostEnvironment env,
            MinecraftAuthService auth, CancellationToken ct) =>
        {
            if (!IsValidKey(uuid) || !IsValidKey(modpackId)) return Results.BadRequest();
            if (await AuthorizeReadAsync(ctx, uuid, auth, ct) is { } denied) return denied;

            var path = Path.Combine(ConfigDir(env, uuid, modpackId), ManifestFile);
            return File.Exists(path) ? Results.File(path, "application/json") : Results.NotFound();
        }).RequireRateLimiting("configs");

        // Bundle: zip só com os caminhos pedidos que existem (o cliente baixa o que lhe falta no pull).
        // Leitura AUTENTICADA (mesmo motivo do manifesto) — e evita o bundle virar amplificador de banda.
        app.MapPost("/players/{uuid}/configs/{modpackId}/bundle", async (
            string uuid, string modpackId, PlayerConfigBundleRequest req, HttpContext ctx,
            IHostEnvironment env, MinecraftAuthService auth, CancellationToken ct) =>
        {
            if (!IsValidKey(uuid) || !IsValidKey(modpackId)) return Results.BadRequest();
            if (await AuthorizeReadAsync(ctx, uuid, auth, ct) is { } denied) return denied;

            var dir = ConfigDir(env, uuid, modpackId);
            var root = Path.GetFullPath(dir);
            if (!Directory.Exists(dir)) return Results.NotFound();

            // Zip para um temporário auto-apagável (DeleteOnClose) e servido por streaming.
            var tmp = Path.Combine(Path.GetTempPath(), "tcmine-bundle-" + Guid.NewGuid().ToString("N") + ".zip");
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                foreach (var rel in req.Paths ?? [])
                {
                    if (rel == ManifestFile) continue;
                    var full = Path.GetFullPath(Path.Combine(dir, rel));
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) continue;
                    await zip.CreateEntryFromFileAsync(full, rel, ct);
                }

            var stream = new FileStream(tmp, FileMode.Open, FileAccess.Read, FileShare.None, 4096,
                FileOptions.DeleteOnClose);
            return Results.File(stream, "application/zip");
        }).RequireRateLimiting("configs");

        // Push incremental: zip com ficheiros novos/alterados + o novo manifesto. Escrita autenticada.
        app.MapPut("/players/{uuid}/configs/{modpackId}/push", async (
            string uuid, string modpackId, HttpContext ctx, IHostEnvironment env,
            MinecraftAuthService auth, CancellationToken ct) =>
        {
            if (!IsValidKey(uuid) || !IsValidKey(modpackId)) return Results.BadRequest();

            var token = BearerToken(ctx);
            if (token is null) return Results.Unauthorized();
            if (!await auth.AuthorizeAsync(token, uuid, ct)) return Results.StatusCode(403);

            // Sobe o limite de corpo do Kestrel (default 30 MB) só neste pedido.
            var sizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = MaxConfigBytes;
            if (ctx.Request.ContentLength is 0 or > MaxConfigBytes) return Results.BadRequest();

            var dir = ConfigDir(env, uuid, modpackId);
            var root = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);

            // Corpo → temporário (streaming, com corte defensivo).
            var tmp = Path.Combine(Path.GetTempPath(), "tcmine-push-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (await CopyBoundedAsync(ctx.Request.Body, fs, MaxConfigBytes, ct) == 0)
                        return Results.BadRequest();
                }

                using var zip = ZipFile.OpenRead(tmp);

                // O manifesto novo (lista completa de ficheiros) vem dentro do zip.
                var manifestEntry = zip.GetEntry(ManifestFile);
                if (manifestEntry is null) return Results.BadRequest();
                PlayerConfigManifest? incoming;
                await using (var ms = manifestEntry.Open())
                    incoming = await JsonSerializer.DeserializeAsync<PlayerConfigManifest>(ms, Json, ct);
                if (incoming is null) return Results.BadRequest();

                // Cota: rejeita se o conjunto declarado excede o teto por (uuid, modpackId). Barra o
                // enchimento do disco mesmo com token válido / durante o fail-open da validação Mojang.
                var declaredBytes = incoming.Files.Values.Sum(f => f.Size);
                if (declaredBytes > maxSetBytes)
                    return Results.Json(
                        new { error = $"Configs excedem a cota de {maxSetBytes / 1024 / 1024} MB por modpack." },
                        statusCode: StatusCodes.Status413PayloadTooLarge);

                var oldManifest = ReadManifest(dir);

                // Extrai os ficheiros alterados/novos (tudo menos o manifesto), com guarda de zip-slip.
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName == ManifestFile || string.IsNullOrEmpty(entry.Name)) continue;
                    var dest = Path.GetFullPath(Path.Combine(dir, entry.FullName));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, true);
                }

                // Apaga o que saiu do manifesto (ficheiros presentes antes e ausentes agora).
                if (oldManifest is not null)
                    foreach (var gone in oldManifest.Files.Keys.Where(k => !incoming.Files.ContainsKey(k)))
                    {
                        var full = Path.GetFullPath(Path.Combine(dir, gone));
                        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) TryDelete(full);
                    }

                // Grava o manifesto com o instante do servidor (fonte do last-write-wins).
                var now = DateTimeOffset.UtcNow;
                var stored = new PlayerConfigManifest(now, incoming.Files);
                await File.WriteAllTextAsync(Path.Combine(dir, ManifestFile),
                    JsonSerializer.Serialize(stored, Json), ct);

                return Results.Json(new { updatedAt = now });
            }
            catch (InvalidOperationException) // excedeu o teto a meio do stream (sem Content-Length)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            finally
            {
                TryDelete(tmp);
            }
        }).RequireRateLimiting("configs");
    }

    private static string ConfigDir(IHostEnvironment env, string uuid, string modpackId) =>
        Path.Combine(ServerPaths.PlayerConfigs(env.ContentRootPath), uuid, modpackId);

    private static PlayerConfigManifest? ReadManifest(string dir)
    {
        var path = Path.Combine(dir, ManifestFile);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<PlayerConfigManifest>(File.ReadAllText(path), Json); }
        catch { return null; }
    }

    /// <summary>Copia até <paramref name="max"/> bytes; corta (BadRequest a montante via 0? não) lançando.</summary>
    private static async Task<long> CopyBoundedAsync(Stream src, Stream dst, long max, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > max) throw new InvalidOperationException("Config excede o tamanho máximo.");
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return total;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort */ }
    }

    /// <summary>Aceita só chaves simples (defesa; também garante um segmento de path seguro).</summary>
    private static bool IsValidKey(string s)
    {
        return !string.IsNullOrWhiteSpace(s) && s.Length <= 80 &&
               s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    /// <summary>
    /// Autoriza uma leitura (manifest/bundle): exige o token Minecraft do próprio UUID. Devolve o
    /// <see cref="IResult"/> de recusa (401 sem token, 403 se não pertence ao UUID) ou <c>null</c> se
    /// autorizado. Fail-open herdado do <see cref="MinecraftAuthService"/> (Mojang fora → autoriza).
    /// </summary>
    private static async Task<IResult?> AuthorizeReadAsync(
        HttpContext ctx, string uuid, MinecraftAuthService auth, CancellationToken ct)
    {
        var token = BearerToken(ctx);
        if (token is null) return Results.Unauthorized();
        if (!await auth.AuthorizeAsync(token, uuid, ct)) return Results.StatusCode(403);
        return null;
    }

    /// <summary>Extrai o token de "Authorization: Bearer &lt;token&gt;".</summary>
    private static string? BearerToken(HttpContext ctx)
    {
        var h = ctx.Request.Headers.Authorization.ToString();
        return h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? h["Bearer ".Length..].Trim()
            : null;
    }
}
