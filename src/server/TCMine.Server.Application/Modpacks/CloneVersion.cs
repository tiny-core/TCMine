using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Deriva um Draft novo a partir de uma versão existente, copiando os arquivos.
///     Base de "criar nova versão com atualizações": o clone nasce igual, e a
///     ingestão depois substitui só os mods escolhidos (UpsertFile por ProjectSlug).
///     Os bytes não são copiados — o blob no store é content-addressed e partilhado.
/// </summary>
public sealed class CloneVersion(IModpackRepository repository)
{
    public async Task<Result<Guid>> HandleAsync(Guid sourceVersionId, string newVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newVersion))
            return Result<Guid>.Fail("Informe o número da nova versão.");

        var source = await repository.GetVersionAsync(sourceVersionId, ct);
        if (source is null)
            return Result<Guid>.Fail("Versão de origem não encontrada.");

        var clone = new ModpackVersion
        {
            ModpackId = source.ModpackId, Version = newVersion.Trim(), LoaderVersion = source.LoaderVersion
        };

        foreach (var file in source.Files)
        {
            clone.UpsertFile(new ModpackFile
            {
                ModpackVersionId = clone.Id,
                ProjectSlug = file.ProjectSlug,
                Path = file.Path,
                Sha256 = file.Sha256,
                SizeBytes = file.SizeBytes,
                Side = file.Side,
                Optional = file.Optional,
                Origin = file.Origin,
                OriginReference = file.OriginReference
            });
        }

        await repository.AddVersionAsync(clone, ct);

        return Result<Guid>.Success(clone.Id);
    }
}
