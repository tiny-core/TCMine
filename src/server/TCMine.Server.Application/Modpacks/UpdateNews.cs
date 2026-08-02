using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

public sealed class UpdateNews(INewsRepository repository)
{
    public async Task<Result> HandleAsync(
        Guid id, string title, string body, bool isPublished, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Fail("Informe o título.");

        var news = await repository.GetByIdAsync(id, ct);
        if (news is null)
            return Result.Fail("Novidade não encontrada.");

        news.Title = title.Trim();
        news.Body = body ?? "";
        news.IsPublished = isPublished;
        news.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.UpdateAsync(news, ct);
        return Result.Success();
    }
}
