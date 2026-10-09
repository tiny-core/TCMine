using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed partial class ActivityLogRepository(
    IDbContextFactory<TcMineDbContext> factory,
    ILogger<ActivityLogRepository> logger) : IActivityLogRepository
{
    public async Task AddAsync(ActivityEvent entry, CancellationToken ct)
    {
        // Nunca derruba quem chamou (ver a interface): publicar uma versão ou
        // tirar um backup não pode falhar porque este log auxiliar não escreveu.
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            db.ActivityEvents.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWriteFailed(ex, entry.Kind);
        }
    }

    public async Task<IReadOnlyList<ActivityEvent>> ListRecentAsync(int count, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ActivityEvents.AsNoTracking()
            .OrderByDescending(e => e.Id) // GUID v7 = cronológico (SQLite não ordena DateTimeOffset)
            .Take(count)
            .ToListAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível gravar a atividade {Kind} no feed.")]
    private partial void LogWriteFailed(Exception ex, ActivityEventKind kind);
}
