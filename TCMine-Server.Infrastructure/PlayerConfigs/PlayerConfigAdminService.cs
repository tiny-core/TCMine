using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.PlayerConfigs;

/// <summary>
/// Fachada admin para as configs player-owned guardadas em disco (<c>tcmine-data/player-configs/{uuid}/
/// {modpackId}/</c>, ver [[concepts/player-config-sync]]). Enumera os conjuntos com tamanho/contagem/último
/// sync, resolve o nome do modpack pela BD e permite <b>apagar</b> um conjunto ou tudo de um jogador para
/// recuperar disco. Não há tabela: o sync é só filesystem, então esta é a única forma de gerir/limpar.
/// </summary>
public sealed class PlayerConfigAdminService(AppDbContext db, IHostEnvironment env)
{
    // Manifesto guardado ao lado dos ficheiros (não conta como config; guarda o UpdatedAt do último sync)
    private const string ManifestFile = ".tcmine-manifest.json";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _root = ServerPaths.PlayerConfigs(env.ContentRootPath);

    // ── Listagem ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Todos os conjuntos de config em disco (um por <c>(uuid, modpackId)</c>), com o total agregado. A
    /// varredura de tamanho roda fora do contexto do chamador para não bloquear o circuito Blazor.
    /// </summary>
    public async Task<PlayerConfigOverviewDto> ListAsync(CancellationToken ct = default)
    {
        // modpackId (Guid) → nome, para rotular os conjuntos; pastas sem modpack na BD ficam com nome null
        var names = await db.Modpacks.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var sets = await Task.Run(() => ScanSets(names), ct);
        return new PlayerConfigOverviewDto(sets.Sum(s => s.SizeBytes), sets);
    }

    private List<PlayerConfigSetDto> ScanSets(IReadOnlyDictionary<Guid, string> modpackNames)
    {
        var sets = new List<PlayerConfigSetDto>();
        if (!Directory.Exists(_root)) return sets;

        foreach (var userDir in Directory.EnumerateDirectories(_root))
        {
            var uuid = Path.GetFileName(userDir);

            foreach (var setDir in Directory.EnumerateDirectories(userDir))
            {
                var modpackId = Path.GetFileName(setDir);
                var (size, count) = MeasureDir(setDir);
                var name = Guid.TryParse(modpackId, out var g) && modpackNames.TryGetValue(g, out var n) ? n : null;
                sets.Add(new PlayerConfigSetDto(uuid, modpackId, name, size, count, ReadManifestUpdatedAt(setDir)));
            }
        }

        // Ordena por jogador e, dentro, por modpack (nome quando existe) — estável para a UI agrupar
        return sets
            .OrderBy(s => s.Uuid, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.ModpackName ?? s.ModpackId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Tamanho total em bytes e nº de ficheiros de config (exclui o manifesto). Iterativo (sem estourar a
    // pilha) e pulando reparse points, por consistência com as demais medições de disco.
    private static (long Size, int Count) MeasureDir(string path)
    {
        var root = new DirectoryInfo(path);
        if (!root.Exists) return (0, 0);

        long size = 0;
        var count = 0;
        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            FileSystemInfo[] entries;
            try { entries = dir.GetFileSystemInfos(); }
            catch { continue; }

            foreach (var entry in entries)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                if (entry is DirectoryInfo sub)
                {
                    stack.Push(sub);
                }
                else if (entry is FileInfo file)
                {
                    size += file.Length;
                    if (!string.Equals(file.Name, ManifestFile, StringComparison.Ordinal)) count++;
                }
            }
        }

        return (size, count);
    }

    private static DateTimeOffset? ReadManifestUpdatedAt(string setDir)
    {
        var path = Path.Combine(setDir, ManifestFile);
        if (!File.Exists(path)) return null;
        try
        {
            var manifest = JsonSerializer.Deserialize<PlayerConfigManifest>(File.ReadAllText(path), Json);
            return manifest?.UpdatedAt;
        }
        catch
        {
            return null; // manifesto corrompido → sem data (não é motivo para falhar a listagem)
        }
    }

    // ── Remoção ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Apaga um conjunto <c>(uuid, modpackId)</c>. No-op se não existir ou chaves inválidas.</summary>
    public Task DeleteSetAsync(string uuid, string modpackId, CancellationToken ct = default)
    {
        var dir = SafeDir(uuid, modpackId);
        return dir is null ? Task.CompletedTask : Task.Run(() => DeleteDir(dir), ct);
    }

    /// <summary>Apaga TODAS as configs de um jogador (a pasta do uuid). No-op se não existir/uuid inválido.</summary>
    public Task DeletePlayerAsync(string uuid, CancellationToken ct = default)
    {
        var dir = SafeDir(uuid);
        return dir is null ? Task.CompletedTask : Task.Run(() => DeleteDir(dir), ct);
    }

    private static void DeleteDir(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    // Resolve o diretório do jogador (e opcionalmente do modpack) garantindo que fica DENTRO da raiz de
    // player-configs (anti path-traversal) e que os segmentos são chaves simples. Null = inválido.
    private string? SafeDir(string uuid, string? modpackId = null)
    {
        if (!IsValidKey(uuid) || (modpackId is not null && !IsValidKey(modpackId))) return null;

        var baseDir = Path.GetFullPath(_root);
        var full = Path.GetFullPath(modpackId is null
            ? Path.Combine(baseDir, uuid)
            : Path.Combine(baseDir, uuid, modpackId));

        var baseWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar) ? baseDir : baseDir + Path.DirectorySeparatorChar;
        return full.StartsWith(baseWithSep, StringComparison.Ordinal) ? full : null;
    }

    // Aceita só chaves simples (letras/dígitos/-/_): defesa e garantia de segmento de path seguro.
    private static bool IsValidKey(string s)
    {
        return !string.IsNullOrWhiteSpace(s) && s.Length <= 80 &&
               s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }
}
