using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

public sealed class UpdateModpack(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(Guid modpackId, string name, string? summary, CancellationToken ct)
    {
        // Nome é o rótulo exibido ao jogador; pode mudar. Slug (identidade),
        // MinecraftVersion e Loader são imutáveis após a criação (§5) e por isso
        // não entram aqui.
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("Informe um nome.");

        var modpack = await repository.GetByIdAsync(modpackId, ct);
        if (modpack is null)
            return Result.Fail("Modpack não encontrado.");

        modpack.Name = name.Trim();
        modpack.Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

        await repository.UpdateAsync(modpack, ct);
        return Result.Success();
    }
}
