using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

public sealed class DeleteNews(INewsRepository repository)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken ct)
    {
        await repository.RemoveAsync(id, ct);
        return Result.Success();
    }
}