using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

public sealed class ReadOverride(IModpackRepository repository, IBlobStore blobStore)
{
    public async Task<Result<string>> HandleAsync(Guid versionId, string path, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<string>.Fail("Versão não encontrada.");

        var file = version.Files.FirstOrDefault(f =>
            f.Origin == ModFileOrigin.Override
            && f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return Result<string>.Fail("Arquivo não encontrado.");

        await using var stream = await blobStore.OpenAsync(file.Sha256, ct);
        using var reader = new StreamReader(stream);
        return Result<string>.Success(await reader.ReadToEndAsync(ct));
    }
}