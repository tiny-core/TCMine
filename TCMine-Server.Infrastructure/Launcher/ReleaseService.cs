using Microsoft.EntityFrameworkCore;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.Launcher;

/// <summary>
///     Leitura do histórico de releases do launcher (para a página <c>/admin/releases</c>). A compilação
///     em si vive no <see cref="LauncherBuildService" />; aqui só listamos o que já foi publicado.
///     Scoped: usa o <see cref="AppDbContext" />.
/// </summary>
public sealed class ReleaseService(AppDbContext db)
{
    /// <summary>Releases publicadas, mais recentes primeiro.</summary>
    public async Task<List<ReleaseEntity>> ListAsync(CancellationToken ct = default)
    {
        return await db.Releases
            .AsNoTracking()
            .OrderByDescending(r => r.PublishedAt)
            .ToListAsync(ct);
    }
}