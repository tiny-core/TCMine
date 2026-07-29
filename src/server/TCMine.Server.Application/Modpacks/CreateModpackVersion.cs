using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Cria uma versão. Nasce em Draft, vazia; os arquivos vêm depois.
/// </summary>
public sealed class CreateModpackVersion(IModpackRepository repository)
{
    public async Task<Result<Guid>> HandleAsync(CreateModpackVersionCommand command, CancellationToken ct)
    {
        var modpack = await repository.GetByIdAsync(command.ModpackId, ct);

        if (modpack is null)
            return Result<Guid>.Fail("Modpack não encontrado.");

        var versionText = command.Version.Trim();

        // Um Draft de cada vez: força terminar e publicar antes de começar a
        // próxima. Evita duas versões meio-feitas em paralelo.
        var versions = await repository.ListVersionsAsync(modpack.Id, ct);
        if (versions.Any(v => v.State is ModpackVersionState.Draft))
            return Result<Guid>.Fail("Já existe uma versão em rascunho. Publique-a antes de criar outra.");

        // O modpack veio com suas versões carregadas (Include), então dá para
        // checar duplicata em memória. O índice único do banco é a garantia
        // final contra corrida.
        if (modpack.Versions.Any(v => v.Version.Equals(versionText, StringComparison.OrdinalIgnoreCase)))
            return Result<Guid>.Fail($"A versão '{versionText}' já existe neste modpack.");

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = versionText,
            LoaderVersion = command.LoaderVersion.Trim(),
            RecommendedMemoryMb = command.RecommendedMemoryMb
        };

        await repository.AddVersionAsync(version, ct);

        return Result<Guid>.Success(version.Id);
    }
}

public sealed record CreateModpackVersionCommand(
    Guid ModpackId,
    string Version,
    string MinecraftVersion,
    ModLoader Loader,
    string LoaderVersion,
    int? RecommendedMemoryMb);