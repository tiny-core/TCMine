using Microsoft.EntityFrameworkCore;
using TCMine_Data.Data;
using TCMine_Data.Entities;

namespace TCMine_Services.Launcher;

/// <summary>
/// Leitura/escrita das configs do jogador na BD, por <c>(uuid, modpackId)</c>. São settings de
/// jogo (keybinds, shaders, minimapa) guardadas como um zip, para repor quando o jogador entra
/// noutro PC. <b>Last-write-wins</b>: cada upsert atualiza <see cref="PlayerConfigEntity.UpdatedAt"/>.
///
/// Scoped — usa o <see cref="AppDbContext"/> da requisição (que também é scoped).
/// </summary>
public sealed class PlayerConfigStore(AppDbContext db)
{
    public Task<PlayerConfigEntity?> GetAsync(string uuid, string modpackId, CancellationToken ct = default)
    {
        return db.PlayerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Uuid == uuid && p.ModpackId == modpackId, ct);
    }

    /// <summary>Grava (insere ou substitui) o zip de configs e devolve o instante da gravação.</summary>
    public async Task<DateTime> UpsertAsync(string uuid, string modpackId, byte[] zip, CancellationToken ct = default)
    {
        var existing = await db.PlayerConfigs
            .FirstOrDefaultAsync(p => p.Uuid == uuid && p.ModpackId == modpackId, ct);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            db.PlayerConfigs.Add(new PlayerConfigEntity
            {
                Uuid = uuid, ModpackId = modpackId, UpdatedAt = now
            });
        }
        else
        {
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return now;
    }
}