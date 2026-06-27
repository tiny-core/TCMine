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

    // Arquivos da instância que o painel pode editar como texto (safelist contra path traversal)
    private static readonly HashSet<string> EditableFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "server.properties", "whitelist.json", "ops.json", "banned-players.json",
        "banned-ips.json", "user_jvm_args.txt"
    };

    // ── Listagem / leitura ──────────────────────────────────────────────────────────────────────────

    // Projeção de linha reutilizada pelas duas listas. "Desatualizada" (IsStale) = provisionada E o
    // modpack mudou desde a última provisão (UpdatedAt > ProvisionedAt). Expression para o EF traduzir.
    private static readonly System.Linq.Expressions.Expression<Func<ServerInstanceEntity, ServerInstanceRowDto>>
        RowProjection = i => new ServerInstanceRowDto(
            i.Id, i.Name, i.ModpackId, i.Modpack!.Name, i.Status, i.Port, i.RamMb,
            i.ProvisionedAt != null, i.AutoRestart,
            i.ProvisionedAt != null && i.Modpack.UpdatedAt > i.ProvisionedAt);

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

    public Task ProvisionAsync(Guid id, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        return provisioner.ProvisionAsync(id, progress, ct);
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

    // ── Edição de arquivos de config da instância ─────────────────────────────────────────────────────

    /// <summary>Arquivos de config editáveis presentes no diretório da instância (subconjunto da safelist).</summary>
    public IReadOnlyList<string> ListEditableFiles(Guid id)
    {
        var dir = Path.Combine(ServerPaths.Servers(_root), id.ToString());
        if (!Directory.Exists(dir)) return [];

        return EditableFiles
            .Where(name => File.Exists(Path.Combine(dir, name)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Lê um arquivo de config editável (null se fora da safelist ou inexistente).</summary>
    public async Task<string?> ReadFileAsync(Guid id, string fileName, CancellationToken ct = default)
    {
        var path = SafeFile(id, fileName);
        if (path is null || !File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    /// <summary>Grava um arquivo de config editável. Recusa nomes fora da safelist.</summary>
    public async Task WriteFileAsync(Guid id, string fileName, string content, CancellationToken ct = default)
    {
        var path = SafeFile(id, fileName)
                   ?? throw new InvalidOperationException("Arquivo não editável.");
        await File.WriteAllTextAsync(path, content, ct);
    }

    // Resolve o caminho de um arquivo editável garantindo que está na safelist e dentro do dir da instância
    private string? SafeFile(Guid id, string fileName)
    {
        var name = Path.GetFileName(fileName); // neutraliza qualquer caminho embutido
        if (!EditableFiles.Contains(name)) return null;
        return Path.Combine(ServerPaths.Servers(_root), id.ToString(), name);
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
