using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.FileSystem;
using TCMine_Infrastructure.Persistence;
using TCMine_Infrastructure.Server;

namespace TCMine_Infrastructure.ServerInstances;

/// <summary>
/// Fachada do painel admin para as instâncias de servidor: CRUD da entidade, mais a delegação das
/// operações pesadas para o <see cref="ServerProvisioner"/> (montar o diretório) e o
/// <see cref="DockerMinecraftManager"/> (ciclo de vida do container, console e logs). Mantém a UI
/// fina — a página só conhece este serviço e os DTOs.
/// </summary>
public sealed class ServerInstanceService(
    AppDbContext db,
    ServerProvisioner provisioner,
    DockerMinecraftManager docker,
    MinecraftServerPinger pinger,
    ContentNotifier notifier,
    IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

    // Extensões consideradas texto editável na árvore de config da instância
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".json5", ".jsonc", ".toml", ".cfg", ".conf", ".config", ".properties",
        ".yaml", ".yml", ".snbt", ".nbt", ".ini", ".lang", ".md", ".mcmeta", ".js", ".zs", ".xml", ".csv",
        ".sh", ".bat", ".log"
    };

    // Pastas ocultas na RAIZ da árvore: pesadas/irrelevantes para editar config (jars, mundo, logs)
    private static readonly HashSet<string> HiddenRootFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "libraries", "mods", "world", "logs", ".tcmine"
    };

    // ── Listagem / leitura ──────────────────────────────────────────────────────────────────────────

    // Projeção de linha reutilizada pelas duas listas. "Desatualizada" (IsStale) = provisionada E o
    // modpack mudou desde a última provisão (UpdatedAt > ProvisionedAt). Expression para o EF traduzir.
    private static readonly System.Linq.Expressions.Expression<Func<ServerInstanceEntity, ServerInstanceRowDto>>
        RowProjection = i => new ServerInstanceRowDto(
            i.Id, i.Name, i.ModpackId, i.Modpack!.Name, i.Status, i.Port, i.RamMb,
            i.ProvisionedAt != null, i.AutoRestart,
            i.ProvisionedAt != null && i.Modpack.UpdatedAt > i.ProvisionedAt,
            i.PublicAddress);

    /// <summary>Linhas da tabela de instâncias (ordenadas por nome).</summary>
    public async Task<List<ServerInstanceRowDto>> ListAsync(CancellationToken ct = default)
    {
        await docker.ReconcileAllAsync(ct); // status reflete a realidade do daemon ao abrir a lista
        return await db.ServerInstances
            .AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(RowProjection)
            .ToListAsync(ct);
    }

    /// <summary>Instâncias derivadas de um modpack específico (para o hub do modpack).</summary>
    public async Task<List<ServerInstanceRowDto>> ListByModpackAsync(Guid modpackId, CancellationToken ct = default)
    {
        return await db.ServerInstances
            .AsNoTracking()
            .Where(i => i.ModpackId == modpackId)
            .OrderBy(i => i.Name)
            .Select(RowProjection)
            .ToListAsync(ct);
    }

    /// <summary>Modpacks disponíveis como origem de uma instância (seletor do diálogo).</summary>
    public async Task<List<ModpackOptionDto>> ListModpackOptionsAsync(CancellationToken ct = default)
    {
        return await db.Modpacks
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new ModpackOptionDto(
                m.Id, m.Name, ModLoaders.DisplayName(m.Loader), m.Minecraft))
            .ToListAsync(ct);
    }

    /// <summary>Estado completo de uma instância para a página de detalhe (ou null se não existir).</summary>
    public async Task<ServerInstanceDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        await docker.ReconcileAllAsync(ct); // status real ao abrir o detalhe
        var i = await db.ServerInstances.AsNoTracking()
            .Include(x => x.Modpack)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null) return null;

        var stale = i.ProvisionedAt != null && i.Modpack!.UpdatedAt > i.ProvisionedAt;
        return new ServerInstanceDetailDto(
            ToEdit(i), i.Modpack!.Name, i.Status, i.ContainerId, i.ProvisionedAt != null, stale);
    }

    // ── CRUD ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Cria a instância (só persistência; provisão e container são passos separados).</summary>
    public async Task<Guid> CreateAsync(ServerInstanceEditDto dto, CancellationToken ct = default)
    {
        var instance = new ServerInstanceEntity { Id = Guid.NewGuid() };
        Apply(instance, dto);
        db.ServerInstances.Add(instance);
        await SyncAdvertisementAsync(instance, ct);
        await db.SaveChangesAsync(ct);
        notifier.Bump(); // divulgação pode ter mudado → avisa os launchers (SSE)
        return instance.Id;
    }

    /// <summary>Atualiza os campos editáveis de uma instância existente.</summary>
    public async Task UpdateAsync(ServerInstanceEditDto dto, CancellationToken ct = default)
    {
        var instance = await db.ServerInstances.FirstOrDefaultAsync(i => i.Id == dto.Id, ct)
                       ?? throw new InvalidOperationException("Instância não encontrada.");
        Apply(instance, dto);
        instance.UpdatedAt = DateTime.UtcNow;
        await SyncAdvertisementAsync(instance, ct);
        await db.SaveChangesAsync(ct);
        notifier.Bump(); // divulgação pode ter mudado → avisa os launchers (SSE)
    }

    /// <summary>
    /// Apaga a instância: remove o container (se houver), o diretório provisionado e a linha. Os caches
    /// compartilhados (mods, instalação do loader) permanecem — servem a outras instâncias.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var instance = await db.ServerInstances.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (instance is null) return;

        // Remove o container (idempotente; ignora se já não existe)
        try { await docker.RemoveContainerAsync(id, ct); } catch { /* daemon offline / já removido */ }

        // Remove a entrada divulgada auto-gerada por esta instância (some do launcher junto)
        var advertised = await db.Servers.Where(s => s.ServerInstanceId == id).ToListAsync(ct);
        db.Servers.RemoveRange(advertised);

        db.ServerInstances.Remove(instance);
        await db.SaveChangesAsync(ct);
        notifier.Bump(); // entrada divulgada removida → avisa os launchers (SSE)

        var dir = Path.Combine(ServerPaths.Servers(_root), id.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    // ── Operações pesadas (delegadas) ────────────────────────────────────────────────────────────────

    public async Task ProvisionAsync(Guid id, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await provisioner.ProvisionAsync(id, progress, ct);

        // Re-provisão muda loader/mods/libraries — o container existente foi criado com o comando antigo
        // (launch args do loader anterior). Remove-o para o próximo start recriar com o estado novo.
        try { await docker.RemoveContainerAsync(id, ct); }
        catch { /* sem container / daemon offline — o próximo start cria do zero mesmo */ }
    }

    /// <summary>
    /// Aplica a atualização do modpack numa instância já provisionada: re-provisiona (re-monta loader/
    /// mods/configs e descarta o container antigo) e, se o servidor <b>estava rodando</b>, sobe de novo
    /// com o estado novo — tudo num clique.
    /// </summary>
    public async Task ApplyUpdateAsync(Guid id, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var wasRunning = await db.ServerInstances.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.Status)
            .FirstOrDefaultAsync(ct) == ServerInstanceStatus.Running;

        await ProvisionAsync(id, progress, ct);
        if (wasRunning) await StartAsync(id, ct); // recria o container e sobe com o loader/mods novos
    }

    public Task StartAsync(Guid id, CancellationToken ct = default)
    {
        return docker.StartAsync(id, ct);
    }

    public Task StopAsync(Guid id, CancellationToken ct = default)
    {
        return docker.StopAsync(id, ct);
    }

    /// <summary>Reinicia: para e sobe de novo (mantém o container, só recicla o processo).</summary>
    public async Task RestartAsync(Guid id, CancellationToken ct = default)
    {
        await docker.StopAsync(id, ct);
        await docker.StartAsync(id, ct);
    }

    public Task RemoveContainerAsync(Guid id, CancellationToken ct = default)
    {
        return docker.RemoveContainerAsync(id, ct);
    }

    public Task SendCommandAsync(string containerId, string command, CancellationToken ct = default)
    {
        return docker.SendCommandAsync(containerId, command, ct);
    }

    public IAsyncEnumerable<string> StreamLogsAsync(
        string containerId, bool follow = true, string tail = "200", CancellationToken ct = default)
    {
        return docker.StreamLogsAsync(containerId, follow, tail, ct);
    }

    /// <summary>
    /// Server List Ping direto (host/porta): jogadores online/máximo, ou <c>null</c> se não respondeu
    /// (ainda subindo / fora). Recebe os dados prontos (não toca na BD) — o detalhe faz polling no
    /// circuito Blazor sem disputar o DbContext scoped. Host vazio cai no loopback (porta publicada no host).
    /// </summary>
    public Task<ServerPing?> PingAsync(string? host, int port, CancellationToken ct = default)
    {
        var target = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        return pinger.PingAsync(target, port, ct);
    }

    /// <summary>
    /// IP local de saída do host (o que os jogadores na mesma rede usam) — para pré-preencher o endereço
    /// público ao criar uma instância. Truque: um socket UDP "conecta" a um IP público (sem enviar nada)
    /// e o SO escolhe a interface de saída. Devolve <c>""</c> se não conseguir descobrir.
    /// </summary>
    public static string GetLocalHostAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530); // não envia pacotes; só resolve a interface de saída
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    // ── Árvore de config da instância (editor de arquivos) ──────────────────────────────────────────────

    /// <summary>É um arquivo de texto editável? (pela extensão).</summary>
    public bool IsConfigText(string path)
    {
        return TextExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// Filhos diretos de uma pasta do diretório da instância (lazy, para a árvore). Pastas primeiro,
    /// depois arquivos. Na raiz, oculta pastas pesadas/irrelevantes (<see cref="HiddenRootFolders"/>).
    /// </summary>
    public IReadOnlyList<OverrideNodeDto> ListConfigChildren(Guid id, string folder)
    {
        var dir = string.IsNullOrEmpty(folder) ? InstanceDir(id) : SafeConfigPath(id, folder);
        if (dir is null || !Directory.Exists(dir)) return [];

        var prefix = string.IsNullOrEmpty(folder) ? "" : folder.TrimEnd('/') + "/";
        var atRoot = string.IsNullOrEmpty(folder);

        var folders = Directory.EnumerateDirectories(dir)
            .Select(d => Path.GetFileName(d)!)
            .Where(name => !atRoot || !HiddenRootFolders.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new OverrideNodeDto(prefix + name, name, true));

        var files = Directory.EnumerateFiles(dir)
            .Select(f => Path.GetFileName(f))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new OverrideNodeDto(prefix + name, name, false));

        return folders.Concat(files).ToList();
    }

    /// <summary>Lê um arquivo de config (null se fora do diretório da instância ou inexistente).</summary>
    public async Task<string?> ReadConfigAsync(Guid id, string path, CancellationToken ct = default)
    {
        var full = SafeConfigPath(id, path);
        if (full is null || !File.Exists(full)) return null;
        return await File.ReadAllTextAsync(full, ct);
    }

    /// <summary>Grava um arquivo de config de texto (cria os diretórios pais). Recusa caminho inválido/binário.</summary>
    public async Task WriteConfigAsync(Guid id, string path, string content, CancellationToken ct = default)
    {
        var full = SafeConfigPath(id, path) ?? throw new InvalidOperationException("Caminho inválido.");
        if (!IsConfigText(path)) throw new InvalidOperationException("Arquivo não editável como texto.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content, ct);
    }

    /// <summary>Cria um arquivo de config novo (vazio). Lança se já existir.</summary>
    public async Task CreateConfigAsync(Guid id, string path, CancellationToken ct = default)
    {
        var full = SafeConfigPath(id, path) ?? throw new InvalidOperationException("Caminho inválido.");
        if (File.Exists(full)) throw new InvalidOperationException("Já existe um arquivo nesse caminho.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, string.Empty, ct);
    }

    /// <summary>Apaga um arquivo de config (no-op se não existir).</summary>
    public Task DeleteConfigAsync(Guid id, string path, CancellationToken ct = default)
    {
        var full = SafeConfigPath(id, path);
        if (full is not null && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string InstanceDir(Guid id)
    {
        return Path.Combine(ServerPaths.Servers(_root), id.ToString());
    }

    // Resolve um caminho relativo garantindo que fica DENTRO do diretório da instância (anti path-traversal)
    private string? SafeConfigPath(Guid id, string relativePath)
    {
        var baseDir = Path.GetFullPath(InstanceDir(id));
        var full = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        var baseWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar) ? baseDir : baseDir + Path.DirectorySeparatorChar;
        return full.StartsWith(baseWithSep, StringComparison.Ordinal) ? full : null;
    }

    // ── Auto-divulgação (instância → entrada do launcher) ─────────────────────────────────────────────

    /// <summary>
    /// Mantém em sync a entrada divulgada (<see cref="ServerEntryEntity"/>) gerada por esta instância:
    /// faz upsert quando <c>Advertise</c> está ligado e há <c>PublicAddress</c>; remove caso contrário.
    /// Só estagia as mudanças no contexto — o <c>SaveChanges</c> fica com o chamador (mesma transação).
    /// </summary>
    private async Task SyncAdvertisementAsync(ServerInstanceEntity instance, CancellationToken ct)
    {
        var entry = await db.Servers.FirstOrDefaultAsync(s => s.ServerInstanceId == instance.Id, ct);
        var shouldAdvertise = instance.Advertise && !string.IsNullOrWhiteSpace(instance.PublicAddress);

        if (!shouldAdvertise)
        {
            if (entry is not null) db.Servers.Remove(entry);
            return;
        }

        if (entry is null)
        {
            db.Servers.Add(new ServerEntryEntity
            {
                ServerInstanceId = instance.Id, ModpackId = instance.ModpackId,
                Name = instance.Name, Address = instance.PublicAddress, Port = instance.Port
            });
            return;
        }

        // Atualiza in-place (nome/endereço/porta/modpack podem ter mudado)
        entry.ModpackId = instance.ModpackId;
        entry.Name = instance.Name;
        entry.Address = instance.PublicAddress;
        entry.Port = instance.Port;
    }

    // ── Mapeamento entidade ↔ DTO ─────────────────────────────────────────────────────────────────────

    private static ServerInstanceEditDto ToEdit(ServerInstanceEntity i)
    {
        return new ServerInstanceEditDto(
            i.Id, i.Name, i.ModpackId, i.Port, i.RamMb, i.XmsMb, i.MaxPlayers, i.Motd,
            i.ExtraJvmArgs, i.AutoRestart, i.PublicAddress, i.Advertise);
    }

    private static void Apply(ServerInstanceEntity i, ServerInstanceEditDto dto)
    {
        i.Name = dto.Name;
        i.ModpackId = dto.ModpackId;
        i.Port = dto.Port;
        i.RamMb = dto.RamMb;
        i.XmsMb = dto.XmsMb;
        i.MaxPlayers = dto.MaxPlayers;
        i.Motd = dto.Motd;
        i.ExtraJvmArgs = dto.ExtraJvmArgs;
        i.AutoRestart = dto.AutoRestart;
        i.PublicAddress = dto.PublicAddress;
        i.Advertise = dto.Advertise;
    }
}
