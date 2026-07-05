using Microsoft.EntityFrameworkCore;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>
///     CRUD da newsletter. Cada notícia tem uma FK <b>opcional</b> ao modpack
///     (<see cref="NewsEntity.ModpackId" />): preenchida = notícia do modpack; nula = notícia
///     <b>global</b> (do servidor). A página de novidades globais usa <see cref="ListAllAsync" />; a aba
///     de um modpack usa <see cref="ListForModpackAsync" />.
///     Diferente do editor de metadados (escrita-só-ao-Guardar), a newsletter grava direto no banco —
///     é conteúdo independente do rascunho do modpack. Cada mutação avisa os launchers (SSE).
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

    /// <summary>Todas as novidades (globais + de modpacks) com o nome do modpack — para o painel global.</summary>
    public async Task<List<NewsRowDto>> ListAllAsync(CancellationToken ct = default)
    {
        return await db.News
            .AsNoTracking()
            .OrderByDescending(n => n.PublishedAt)
            .Select(n => new NewsRowDto(
                n.Id, n.ModpackId,
                // Nome via subconsulta (FK opcional, sem navigation property); null = global
                n.ModpackId == null
                    ? null
                    : db.Modpacks.Where(m => m.Id == n.ModpackId).Select(m => m.Name).FirstOrDefault(),
                n.Tag, n.Title, n.Summary, n.PublishedAt, n.IsPublished))
            .ToListAsync(ct);
    }

    /// <summary>Feed público (só publicadas) para o launcher: globais + de modpacks, mais recentes primeiro.</summary>
    public async Task<List<NewsItemDto>> ListPublishedAsync(CancellationToken ct = default)
    {
        return await db.News
            .AsNoTracking()
            .Where(n => n.IsPublished)
            .OrderByDescending(n => n.PublishedAt)
            .Select(n => new NewsItemDto(
                n.Tag, n.Title, n.Summary, n.PublishedAt, n.ModpackId,
                n.ModpackId == null
                    ? null
                    : db.Modpacks.Where(m => m.Id == n.ModpackId).Select(m => m.Name).FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Modpacks (id + nome) para o seletor opcional do diálogo de novidade.</summary>
    public async Task<List<ModpackBadgeDto>> ListModpackOptionsAsync(CancellationToken ct = default)
    {
        return await db.Modpacks
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new ModpackBadgeDto(m.Id, m.Name))
            .ToListAsync(ct);
    }

    /// <summary>
    ///     Cria uma notícia. <paramref name="draft" />.<c>ModpackId</c> nulo = global; preenchido = do
    ///     modpack. Devolve a entidade gravada (com Id).
    /// </summary>
    public async Task<NewsEntity> CreateAsync(NewsEntity draft, CancellationToken ct = default)
    {
        var entity = new NewsEntity
        {
            ModpackId = draft.ModpackId,
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

    /// <summary>Cria uma notícia atrelada a um modpack específico (usado pela aba do editor).</summary>
    public Task<NewsEntity> CreateAsync(Guid modpackId, NewsEntity draft, CancellationToken ct = default)
    {
        draft.ModpackId = modpackId;
        return CreateAsync(draft, ct);
    }

    /// <summary>Atualiza uma notícia existente (inclui o vínculo de modpack). No-op se não existir.</summary>
    public async Task UpdateAsync(NewsEntity draft, CancellationToken ct = default)
    {
        var row = await db.News.FirstOrDefaultAsync(n => n.Id == draft.Id, ct);
        if (row is null) return;

        row.ModpackId = draft.ModpackId;
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