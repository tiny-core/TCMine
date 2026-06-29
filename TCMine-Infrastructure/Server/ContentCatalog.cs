using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.Launcher;
using TCMine_Infrastructure.Persistence;

namespace TCMine_Infrastructure.Server;

/// <summary>
///     Fonte de conteúdo do site público (landing) e de estatísticas para o painel: lê os modpacks
///     publicados (com seus servidores) e contagens diretamente do banco, e reflete o estado do feed
///     Velopack do launcher (via <see cref="LauncherFeedService" />).
///     Singleton: como o <see cref="AppDbContext" /> é scoped, abre um escopo curto por consulta através
///     do <see cref="IServiceScopeFactory" /> (mesmo padrão de <c>ServerSettingsService</c>).
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
    ///     Agregado completo para o dashboard do painel: contagens, distribuição de mods,
    ///     estado das instâncias, última release, modpacks recentes e atividade recente.
    ///     Reúne tudo num único escopo de DB para evitar várias idas e voltas a partir da página.
    /// </summary>
    public async Task<DashboardData> GetDashboardAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Contagens-base de conteúdo
        var modpacks = await db.Modpacks.CountAsync(ct);
        var published = await db.Modpacks.CountAsync(m => m.IsPublished, ct);
        // "Mods" = arquivos únicos (compartilhados entre modpacks); a distribuição abaixo conta
        // os vínculos por-modpack (um mesmo arquivo em 2 packs conta 2x na distribuição).
        var mods = await db.ModFiles.CountAsync(ct);
        var servers = await db.Servers.CountAsync(ct);
        var users = await db.Users.CountAsync(ct);

        // Novidades: globais (sem modpack) vs. atreladas a um modpack — só as publicadas
        var globalNews = await db.News.CountAsync(n => n.IsPublished && n.ModpackId == null, ct);
        var modpackNews = await db.News.CountAsync(n => n.IsPublished && n.ModpackId != null, ct);

        // Distribuição de mods por lado (sobre os vínculos por-modpack) — "Both" conta para os dois
        var clientMods = await db.ModpackMods.CountAsync(m => m.Side == ModSide.Client, ct);
        var serverMods = await db.ModpackMods.CountAsync(m => m.Side == ModSide.Server, ct);
        var sharedMods = await db.ModpackMods.CountAsync(m => m.Side == ModSide.Both, ct);

        // Instâncias de servidor gerenciadas e quantas estão de pé agora
        var instances = await db.ServerInstances.CountAsync(ct);
        var runningInstances = await db.ServerInstances
            .CountAsync(s => s.Status == ServerInstanceStatus.Running, ct);
        // Desatualizadas: provisionadas cujo modpack mudou desde então (precisam re-provisionar)
        var staleInstances = await db.ServerInstances
            .CountAsync(s => s.ProvisionedAt != null && s.Modpack!.UpdatedAt > s.ProvisionedAt, ct);

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
                h.ModpackId,
                // Nome do modpack via sub consulta (não há navigation property); null se já foi excluído
                db.Modpacks.Where(m => m.Id == h.ModpackId).Select(m => m.Name).FirstOrDefault(),
                h.Operation, h.PathBefore, h.PathAfter, h.Actor, h.CreatedAt))
            .ToListAsync(ct);

        return new DashboardData(
            modpacks,
            published,
            mods,
            clientMods,
            serverMods,
            sharedMods,
            servers,
            users,
            globalNews,
            modpackNews,
            instances,
            runningInstances,
            staleInstances,
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
    int SharedMods,
    int Servers,
    int Users,
    int GlobalNews,
    int ModpackNews,
    int Instances,
    int RunningInstances,
    int StaleInstances,
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
    string? ModpackName,
    OverrideOp Operation,
    string? PathBefore,
    string? PathAfter,
    string? Actor,
    DateTime CreatedAt);