using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Modpacks;

public sealed class UpdateModpack(IModpackRepository repository, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid modpackId, string name, string? summary, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(modpackId, ModpackAccessPolicy.CanEdit, ct);
        if (!auth.Succeeded)
            return auth;

        // Nome é o rótulo exibido ao jogador; pode mudar. Slug (identidade),
        // MinecraftVersion e Loader são imutáveis após a criação (§5) e por isso
        // não entram aqui.
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("Informe um nome.");

        // Só para saber que existe: a linha do modpack, sem as versões.
        if (await repository.GetHeaderAsync(modpackId, ct) is null)
            return Result.Fail("Modpack não encontrado.");

        // Só as duas colunas que esta tela edita. Gravar o modpack inteiro
        // regravava todas as versões dele (ver IModpackRepository.UpdateDetailsAsync).
        await repository.UpdateDetailsAsync(
            modpackId, name.Trim(), string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(), ct);
        return Result.Success();
    }
}
