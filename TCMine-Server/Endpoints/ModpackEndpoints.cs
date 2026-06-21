using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using TCMine_Core.Contracts;
using TCMine_Core.Infrastructure;
using TCMine_Core.modpack;
using TCMine_Data.Data;
using TCMine_Services.Server;

namespace TCMine_Server.Endpoints;

/// <summary>
/// Endpoints HTTP consumidos pelo <b>launcher</b> (não pela admin Blazor): catálogo de modpacks,
/// manifesto detalhado, serving dos jars do cache e do bundle de overrides.
///
/// Princípio central: os mods são servidos pelo <b>próprio servidor</b> — o manifesto reescreve a
/// URL de cada mod para <c>/files/{fileId}/{fileName}</c>, e o launcher baixa daqui, nunca do
/// CurseForge (ver project-modpack-mods-locais). O manifesto público é filtrado para o lado cliente.
/// </summary>
public static class ModpackEndpoints
{
    public static void MapModpackEndpoints(this IEndpointRouteBuilder app)
    {
        // Catálogo público (resumos) — só modpacks publicados
        app.MapGet("/api/modpacks", async (AppDbContext db, CancellationToken ct) =>
        {
            var packs = await db.Modpacks
                .AsNoTracking()
                .Where(m => m.IsPublished)
                .OrderBy(m => m.Name)
                .Select(m => new ModpackSummaryDto(
                    m.Id, m.Name, m.Version, m.Minecraft, m.Loader, m.LoaderVersion,
                    m.Description,
                    m.Mods.Count(x => x.Side == ModSide.Both || x.Side == ModSide.Client),
                    m.Servers.Count,
                    m.UpdatedAt))
                .ToListAsync(ct);

            return Results.Ok(packs);
        });

        // Manifesto detalhado — mods do lado cliente, com URL apontando para o servidor
        app.MapGet("/api/modpacks/{uid:guid}", async (Guid uid, AppDbContext db, HttpContext ctx,
            ServerSettingsService settings, CancellationToken ct) =>
        {
            var pack = await db.Modpacks
                .AsNoTracking()
                .Include(m => m.Mods)
                .Include(m => m.Servers)
                .FirstOrDefaultAsync(m => m.Id == uid && m.IsPublished, ct);

            if (pack is null) return Results.NotFound();

            var baseUrl = await ResolveBaseUrlAsync(ctx, settings, ct);

            // Só os mods que rodam no cliente entram no manifesto do launcher
            var mods = pack.Mods
                .Where(m => ModSideRules.RunsOnClient(m.Side))
                .Select(m => new ModDto(
                    m.CurseModId, m.FileId, m.Name, m.FileName,
                    $"{baseUrl}/files/{m.FileId}/{Uri.EscapeDataString(m.FileName)}",
                    m.Target, m.Version))
                .ToList();

            var servers = pack.Servers
                .Select(s => new ServerDto(s.Name, s.Address, s.Port))
                .ToList();

            var manifest = new ModpackManifestDto(
                pack.Id, pack.Name, pack.Version, pack.Minecraft, pack.Loader, pack.LoaderVersion,
                pack.Description, pack.HasOverrides, pack.RecommendedRamMb, mods, servers);

            return Results.Ok(manifest);
        });

        // Serving de um jar do cache compartilhado (tcmine-data/mods/{fileId}/{fileName})
        app.MapGet("/files/{fileId:long}/{fileName}", (long fileId, string fileName,
            IHostEnvironment env) =>
        {
            // Path.GetFileName neutraliza tentativas de path traversal no nome do arquivo
            var safeName = Path.GetFileName(fileName);
            var path = Path.Combine(ServerPaths.Mods(env.ContentRootPath), fileId.ToString(), safeName);

            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(path, "application/java-archive", safeName);
        });

        // Bundle de overrides (zip montado sob demanda a partir da pasta extraída)
        app.MapGet("/api/modpacks/{uid:guid}/overrides.zip", async (Guid uid, AppDbContext db,
            IHostEnvironment env, CancellationToken ct) =>
        {
            var exists = await db.Modpacks
                .AsNoTracking()
                .AnyAsync(m => m.Id == uid && m.IsPublished && m.HasOverrides, ct);
            if (!exists) return Results.NotFound();

            var dir = Path.Combine(ServerPaths.Modpacks(env.ContentRootPath), uid.ToString(), "overrides");
            if (!Directory.Exists(dir)) return Results.NotFound();

            // Re-empacota em memória — o conteúdo em disco é a fonte editável pelo painel
            var ms = new MemoryStream();
            await using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    await zip.CreateEntryFromFileAsync(file, rel, ct);
                }
            }

            ms.Position = 0;
            return Results.File(ms, "application/zip", $"{uid}-overrides.zip");
        });
    }

    /// <summary>
    /// URL base externa para montar links absolutos de download. Usa o <c>PublicBaseUrl</c>
    /// configurado (canônico atrás de proxy reverso); na falta dele, deriva da requisição.
    /// </summary>
    private static async Task<string> ResolveBaseUrlAsync(
        HttpContext ctx, ServerSettingsService settings, CancellationToken ct)
    {
        var configured = (await settings.GetStoredAsync(ct)).PublicBaseUrl;
        return !string.IsNullOrWhiteSpace(configured) ? configured.TrimEnd('/') : $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    }
}