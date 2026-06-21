using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine_Application.Contracts;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.Persistence;
using TCMine_Domain.Entities;
using TCMine_Infrastructure.Launcher;

namespace TCMine_Infrastructure.Server;

/// <summary>
/// Fonte de conteúdo do site público (landing) e de estatísticas para o painel: lê os modpacks
/// publicados (com seus servidores) e contagens diretamente do banco, e reflete o estado do feed
/// Velopack do launcher (via <see cref="LauncherFeedService"/>).
///
/// Singleton: como o <see cref="AppDbContext"/> é scoped, abre um escopo curto por consulta através
/// do <see cref="IServiceScopeFactory"/> (mesmo padrão de <c>ServerSettingsService</c>).
/// </summary>
public sealed class ContentCatalog(LauncherFeedService feed, IServiceScopeFactory scopeFactory)
{
    // Versão do launcher disponível para download (lida do feed Velopack); null = ainda não publicado
    public string? LauncherVersion => feed.LatestVersion();

    // Indica se há um instalador pronto para baixar (Setup.exe no feed Velopack)
    public bool LauncherAvailable => feed.HasInstaller();

    /// <summary>Modpacks publicados (com seus servidores), ordenados por nome — para a landing.</summary>
    public async Task<IReadOnlyList<ModpackWithServers>> GetModpacksAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Modpacks
            .AsNoTracking()
            .Where(m => m.IsPublished)
            .OrderBy(m => m.Name)
            .Select(m => new ModpackWithServers(
                new ModpackSummaryDto(
                    m.Id, m.Name, m.Version, m.Minecraft, m.Loader, m.LoaderVersion,
                    m.Description,
                    // Contagem do lado cliente (o que o jogador instala), igual ao manifesto público
                    m.Mods.Count(x => x.Side == ModSide.Both || x.Side == ModSide.Client),
                    m.Servers.Count,
                    m.UpdatedAt),
                m.Servers
                    .Select(s => new ServerDto(s.Name, s.Address, s.Port))
                    .ToList()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Agregado completo para o dashboard do painel: contagens, distribuição de mods,
    /// estado das instâncias, última release, modpacks recentes e atividade recente.
    /// Reúne tudo num único escopo de DB para evitar várias idas e voltas a partir da página.
    /// </summary>
    public async Task<DashboardData> GetDashboardAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Contagens-base de conteúdo
        var modpacks = await db.Modpacks.CountAsync(ct);
        var published = await db.Modpacks.CountAsync(m => m.IsPublished, ct);
        var mods = await db.Mods.CountAsync(ct);
        var servers = await db.Servers.CountAsync(ct);
        var users = await db.Users.CountAsync(ct);

        // Distribuição de mods por lado — "Both" conta para cliente e servidor (regra do manifesto)
        var clientMods = await db.Mods.CountAsync(m => m.Side == ModSide.Both || m.Side == ModSide.Client, ct);
        var serverMods = await db.Mods.CountAsync(m => m.Side == ModSide.Both || m.Side == ModSide.Server, ct);

        // Instâncias de servidor gerenciadas e quantas estão de pé agora
        var instances = await db.ServerInstances.CountAsync(ct);
        var runningInstances = await db.ServerInstances
            .CountAsync(s => s.Status == ServerInstanceStatus.Running, ct);

        // Releases publicadas + a mais recente (para o card de versão e o changelog rápido)
        var releases = await db.Releases.CountAsync(ct);
        var latestRelease = await db.Releases
            .AsNoTracking()
            .OrderByDescending(r => r.PublishedAt)
            .Select(r => new { r.Version, r.PublishedAt })
            .FirstOrDefaultAsync(ct);

        // Últimos modpacks tocados — ordenados pela data de modificação (sync incremental)
        var recentModpacks = await db.Modpacks
            .AsNoTracking()
            .OrderByDescending(m => m.UpdatedAt)
            .Take(5)
            .Select(m => new RecentModpack(
                m.Id, m.Name, m.Version, m.Loader, m.IsPublished, m.Mods.Count, m.UpdatedAt))
            .ToListAsync(ct);

        // Trilha de auditoria — últimas alterações de overrides (quem mexeu em quê)
        var recentActivity = await db.OverrideHistory
            .AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .Take(6)
            .Select(h => new ActivityItem(
                h.ModpackId, h.Operation, h.PathBefore, h.PathAfter, h.Actor, h.CreatedAt))
            .ToListAsync(ct);

        return new DashboardData(
            modpacks,
            published,
            mods,
            clientMods,
            serverMods,
            servers,
            users,
            instances,
            runningInstances,
            releases,
            latestRelease?.Version,
            latestRelease?.PublishedAt,
            recentModpacks,
            recentActivity);
    }
}

/// <summary>Resumo de um modpack acompanhado da sua lista de servidores.</summary>
public sealed record ModpackWithServers(
    ModpackSummaryDto Summary,
    IReadOnlyList<ServerDto> Servers);

/// <summary>Agregado de tudo o que o dashboard do painel exibe numa única passagem.</summary>
public sealed record DashboardData(
    int Modpacks,
    int PublishedModpacks,
    int Mods,
    int ClientMods,
    int ServerMods,
    int Servers,
    int Users,
    int Instances,
    int RunningInstances,
    int Releases,
    string? LatestReleaseVersion,
    DateTime? LatestReleaseAt,
    IReadOnlyList<RecentModpack> RecentModpacks,
    IReadOnlyList<ActivityItem> RecentActivity);

/// <summary>Linha da lista de "modpacks recentes" do dashboard.</summary>
public sealed record RecentModpack(
    Guid Id,
    string Name,
    string Version,
    ModLoader Loader,
    bool IsPublished,
    int ModCount,
    DateTime UpdatedAt);

/// <summary>Item da timeline de "atividade recente" (uma entrada do histórico de overrides).</summary>
public sealed record ActivityItem(
    Guid ModpackId,
    OverrideOp Operation,
    string? PathBefore,
    string? PathAfter,
    string? Actor,
    DateTime CreatedAt);