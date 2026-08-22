using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Edita os metadados de uma versão em rascunho: número, versão do loader e
///     RAM. Só em Draft — versão publicada é imutável. Mods/overrides editam-se
///     na grade, não aqui.
///     A versão do loader é editável de propósito: ela pertence à VERSÃO, e não
///     ao modpack, justamente porque sobe entre versões. Quem errou o número ao
///     criar o rascunho não deveria precisar apagá-lo e recomeçar. O que não
///     muda é a versão do Minecraft e o loader em si — esses ficam no modpack e
///     são imutáveis, porque mod não migra entre eles.
/// </summary>
public sealed class UpdateModpackVersion(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(
        Guid versionId, string version, string loaderVersion, int? recommendedMemoryMb,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(version))
            return Result.Fail("Informe o número da versão.");

        // Obrigatória: é ela que o launcher e o container do servidor usam para
        // instalar o loader. Em branco, a instância não sobe.
        if (string.IsNullOrWhiteSpace(loaderVersion))
            return Result.Fail("Informe a versão do loader.");

        var current = await repository.GetVersionAsync(versionId, ct);
        if (current is null)
            return Result.Fail("Versão não encontrada.");

        if (current.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível editar uma versão em rascunho.");

        var versionText = version.Trim();

        // Se o número mudou, não pode colidir com outra versão do mesmo pack.
        if (!versionText.Equals(current.Version, StringComparison.OrdinalIgnoreCase))
        {
            var siblings = await repository.ListVersionsAsync(current.ModpackId, ct);
            if (siblings.Any(v => v.Id != versionId
                                  && v.Version.Equals(versionText, StringComparison.OrdinalIgnoreCase)))
                return Result.Fail($"A versão '{versionText}' já existe neste modpack.");
        }

        current.Version = versionText;
        current.LoaderVersion = loaderVersion.Trim();
        current.RecommendedMemoryMb = recommendedMemoryMb;

        await repository.UpdateVersionAsync(current, ct);
        return Result.Success();
    }
}
