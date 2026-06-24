using Microsoft.EntityFrameworkCore;
using TCMine_Domain.Entities;
using TCMine_Infrastructure.Persistence;

namespace TCMine_Infrastructure.Server;

/// <summary>
/// CRUD da newsletter de um modpack. Cada notícia tem uma FK opcional ao modpack
/// (<see cref="NewsEntity.ModpackId"/>): aqui só lidamos com as notícias <b>de um modpack</b>
/// (FK preenchida); o feed global (FK nula) é gerido à parte.
///
/// Diferente do editor de metadados (escrita-só-ao-Guardar), a newsletter grava direto no banco —
/// é conteúdo independente do rascunho do modpack. Cada mutação avisa os launchers (SSE).
/// </summary>
public sealed class ModpackNewsService(AppDbContext db, ContentNotifier notifier)
{
    /// <summary>Notícias de um modpack, mais recentes primeiro (projeção rastreável p/ edição).</summary>
    public async Task<List<NewsEntity>> ListForModpackAsync(Guid modpackId, CancellationToken ct = default)
    {
        return await db.News
            .AsNoTracking()
            .Where(n => n.ModpackId == modpackId)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync(ct);
    }

    /// <summary>Cria uma notícia atrelada ao modpack e devolve a entidade gravada (com Id).</summary>
    public async Task<NewsEntity> CreateAsync(Guid modpackId, NewsEntity draft, CancellationToken ct = default)
    {
        var entity = new NewsEntity
        {
            ModpackId = modpackId,
            Tag = draft.Tag,
            Title = draft.Title,
            Summary = draft.Summary,
            PublishedAt = draft.PublishedAt == default ? DateTime.UtcNow : draft.PublishedAt,
            IsPublished = draft.IsPublished
        };

        db.News.Add(entity);
        await db.SaveChangesAsync(ct);
        notifier.Bump();
        return entity;
    }

    /// <summary>Atualiza uma notícia existente (campos editáveis). No-op se não existir.</summary>
    public async Task UpdateAsync(NewsEntity draft, CancellationToken ct = default)
    {
        var row = await db.News.FirstOrDefaultAsync(n => n.Id == draft.Id, ct);
        if (row is null) return;

        row.Tag = draft.Tag;
        row.Title = draft.Title;
        row.Summary = draft.Summary;
        row.PublishedAt = draft.PublishedAt;
        row.IsPublished = draft.IsPublished;

        await db.SaveChangesAsync(ct);
        notifier.Bump();
    }

    /// <summary>Apaga uma notícia pelo Id. No-op se não existir.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var row = await db.News.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (row is null) return;

        db.News.Remove(row);
        await db.SaveChangesAsync(ct);
        notifier.Bump();
    }
}
