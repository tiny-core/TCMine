using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

public sealed class UpdateModpack(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(Guid modpackId, string? summary, CancellationToken ct)
    {
        var modpack = await repository.GetByIdAsync(modpackId, ct);
        if (modpack is null)
            return Result.Fail("Modpack não encontrado.");

        modpack.Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

        await repository.UpdateAsync(modpack, ct);
        return Result.Success();
    }
}