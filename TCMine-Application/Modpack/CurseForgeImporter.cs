using System.IO.Compression;
using System.Text.Json;
using TCMine_Application.Abstractions;
using TCMine_Application.Contracts;
using TCMine_Domain.Modpack;

namespace TCMine_Application.Modpack;

/// <summary>
///     Montagem de um modpack do CurseForge — lógica PURA partilhada por servidor e
///     cliente. Lê o <c>manifest.json</c> do zip, resolve os arquivos/mods (via
///     <see cref="ICurseForgeApi" />) e devolve um <see cref="ImportedModpackDto" /> + o
///     bundle de overrides. O acesso ao CurseForge (key vs proxy) é injetado.
/// </summary>
public abstract class CurseForgeImporter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Importa assincronamente um modpack do CurseForge usando a API fornecida e o identificador do modpack.
    /// </summary>
    public static async Task<ImportedModpackDto?> ImportAsync(
        long modpackId, ICurseForgeApi api, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // 1) Arquivo mais recente do modpack (o .zip).
        progress?.Report("Resolvendo o arquivo do modpack no CurseForge…");
        var packFile = await api.GetLatestFileAsync(modpackId, ct);
        var packUrl = ResolveDownloadUrl(packFile);
        if (packUrl is null) return null;

        // 2) Descarrega e lê o manifest.json + extrai os overrides.
        progress?.Report("Baixando o pacote e lendo o manifesto…");
        using var buffer = new MemoryStream();
        await using (var net = await api.OpenStreamAsync(packUrl, ct))
        {
            await net.CopyToAsync(buffer, ct);
        }

        buffer.Position = 0;

        await using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        var manifestEntry = zip.GetEntry("manifest.json");
        if (manifestEntry is null) return null;

        CfManifestDto? manifest;
        await using (var ms = await manifestEntry.OpenAsync(ct))
        {
            manifest = await JsonSerializer.DeserializeAsync<CfManifestDto>(ms, Json, ct);
        }

        if (manifest is null) return null;

        // 3) Resolve arquivos e mods em lote.
        progress?.Report($"Resolvendo {manifest.Files.Count} mods do manifesto…");
        var fileIds = manifest.Files.Select(f => f.FileId).ToList();
        var files = await api.GetFilesAsync(fileIds, ct);
        var modIds = manifest.Files.Select(f => f.ProjectId).Distinct().ToList();
        var info = await api.GetModsAsync(modIds, ct);

        var mods = new List<ImportedModDto>();
        foreach (var entry in manifest.Files)
        {
            files.TryGetValue(entry.FileId, out var file);
            info.TryGetValue(entry.ProjectId, out var mod);
            var url = ResolveDownloadUrl(file);
            mods.Add(new ImportedModDto(
                entry.ProjectId, entry.FileId,
                mod?.Name ?? file?.FileName ?? $"mod {entry.ProjectId}",
                file?.FileName ?? string.Empty,
                url ?? string.Empty,
                mod is null ? "mod" : ClassToTarget(mod.ClassId),
                file?.DisplayName));
        }

        // Loader primário do manifesto → (tipo, versão). Não fica preso ao NeoForge.
        var loaderRef = manifest.Minecraft.ModLoaders.FirstOrDefault(l => l.Primary)
                        ?? manifest.Minecraft.ModLoaders.FirstOrDefault();
        var (loader, loaderVersion) = ModLoaders.ParseId(loaderRef?.Id);

        var overrides = BuildOverridesZip(zip, manifest.Overrides ?? "overrides");

        return new ImportedModpackDto(
            manifest.Name ?? "Modpack importado",
            manifest.Version ?? "1.0.0",
            manifest.Minecraft.Version,
            loader,
            loaderVersion,
            mods,
            overrides,
            packFile?.ServerPackFileId,
            // Origem CF: o projeto importado e o arquivo (.zip) aplicado
            modpackId,
            packFile?.Id ?? 0);
    }

    /// <summary>
    /// Resolve um único par (mod, arquivo) do CurseForge num <see cref="ImportedModDto" />.
    /// É a base do "adicionar mod manualmente" — partilhada por servidor (admin) e launcher,
    /// porque ambos passam a sua própria <see cref="ICurseForgeApi" /> (direta vs proxy).
    /// </summary>
    public static async Task<ImportedModDto?> ImportSingleAsync(
        long modId, long fileId, ICurseForgeApi api, CancellationToken ct = default)
    {
        var files = await api.GetFilesAsync([fileId], ct);
        if (!files.TryGetValue(fileId, out var file)) return null;

        var mods = await api.GetModsAsync([modId], ct);
        mods.TryGetValue(modId, out var mod);

        var url = ResolveDownloadUrl(file);
        return new ImportedModDto(
            modId, fileId,
            mod?.Name ?? file.FileName,
            file.FileName,
            url ?? string.Empty,
            mod is null ? "mod" : ClassToTarget(mod.ClassId),
            file.DisplayName);
    }

    /// <summary>
    /// Infere o lado (<see cref="ModSide" />) de um mod a partir do conteúdo do server pack.
    /// Regra: o manifesto lista os mods do <b>cliente</b>; o server pack contém o subconjunto
    /// que roda no servidor. Logo, mod presente no pack ⇒ <see cref="ModSide.Both" />; ausente ⇒
    /// <see cref="ModSide.Client" />. Sem server pack (<paramref name="serverPackFileNames" /> nulo),
    /// assume <see cref="ModSide.Both" /> — o admin ajusta manualmente.
    /// </summary>
    public static ModSide InferSide(string modFileName, IReadOnlySet<string>? serverPackFileNames)
    {
        if (serverPackFileNames is null) return ModSide.Both;
        return serverPackFileNames.Contains(modFileName) ? ModSide.Both : ModSide.Client;
    }

    /// <summary>Reempacota a pasta de overrides (sem o prefixo) num zip próprio.</summary>
    public static byte[]? BuildOverridesZip(ZipArchive src, string folder)
    {
        var prefix = folder.TrimEnd('/') + "/";
        var entries = src.Entries
            .Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && !e.FullName.EndsWith('/'))
            .ToList();
        if (entries.Count == 0) return null;

        using var ms = new MemoryStream();
        using (var outZip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var e in entries)
            {
                var rel = e.FullName[prefix.Length..];
                if (string.IsNullOrEmpty(rel)) continue;
                var outEntry = outZip.CreateEntry(rel);
                using var inS = e.Open();
                using var outS = outEntry.Open();
                inS.CopyTo(outS);
            }
        }

        return ms.ToArray();
    }

    /// <summary>Mapeia a classe do CurseForge para a pasta de destino no cliente.</summary>
    public static string ClassToTarget(long classId)
    {
        return classId switch
        {
            12 => "resourcepack",
            6552 => "shaderpack",
            _ => "mod"
        };
    }

    /// <summary>downloadUrl da API, ou reconstrução do URL edge.forgecdn quando vem nulo.</summary>
    public static string? ResolveDownloadUrl(CfFileRefDto? file)
    {
        if (file is null) return null;
        if (!string.IsNullOrWhiteSpace(file.DownloadUrl)) return file.DownloadUrl;
        return string.IsNullOrWhiteSpace(file.FileName)
            ? null
            : $"https://edge.forgecdn.net/files/{file.Id / 1000}/{file.Id % 1000}/{file.FileName}";
    }
}