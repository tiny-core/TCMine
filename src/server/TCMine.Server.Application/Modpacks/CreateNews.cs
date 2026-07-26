using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

public sealed class CreateNews(INewsRepository repository)
{
    public async Task<Result<Guid>> HandleAsync(
        Guid modpackId, string title, string body, bool isPublished, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<Guid>.Fail("Informe o título.");

        var news = new News
        {
            ModpackId = modpackId,
            Title = title.Trim(),
            Body = body ?? "",
            IsPublished = isPublished
        };

        await repository.AddAsync(news, ct);
        return Result<Guid>.Success(news.Id);
    }
}