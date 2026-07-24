using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Cria uma nova versão de um modpack. Ela nasce em Draft, vazia — os
///     arquivos são adicionados depois, e só então ela é publicada.
/// </summary>
public sealed class CreateModpackVersion(IModpackRepository repository)
{
    public async Task<Result<Guid>> HandleAsync(CreateModpackVersionCommand command, CancellationToken ct)
    {
        var modpack = await repository.GetByIdAsync(command.ModpackId, ct);

        if (modpack is null)
            return Result<Guid>.Fail("Modpack não encontrado.");

        var versionText = command.Version.Trim();

        // Duas versões com o mesmo número no mesmo pack seria ambígua para
        // o launcher, que identifica a instância por (modpack, versão). O
        // índice único do banco garante isso, mas checar antes dá mensagem
        // melhor que um erro de constraint.
        if (modpack.Versions.Any(v => v.Version.Equals(versionText, StringComparison.OrdinalIgnoreCase)))
            return Result<Guid>.Fail($"A versão '{versionText}' já existe neste modpack.");

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = versionText,
            MinecraftVersion = command.MinecraftVersion.Trim(),
            Loader = command.Loader,
            LoaderVersion = command.LoaderVersion.Trim(),
            RecommendedMemoryMb = command.RecommendedMemoryMb
        };

        modpack.Versions.Add(version);
        await repository.SaveChangesAsync(ct);

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