using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Anexa um arquivo enviado manualmente pelo admin a uma versão em Draft.
///     Só funciona em Draft: versão publicada é imutável.
/// </summary>
public sealed class AddManualFile(
    IModpackRepository repository,
    IBlobStore blobStore)
{
    public async Task<Result<ModpackFileDto>> HandleAsync(AddManualFileCommand command, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(command.ModpackVersionId, ct);

        if (version is null)
            return Result<ModpackFileDto>.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result<ModpackFileDto>.Fail("Só é possível adicionar arquivos a uma versão em rascunho.");

        var path = NormalizePath(command.Path);
        if (path is null)
            return Result<ModpackFileDto>.Fail("Caminho inválido.");

        if (version.Files.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return Result<ModpackFileDto>.Fail($"Já existe um arquivo em '{path}' nesta versão.");

        // O blob store grava e devolve o hash real, calculado na escrita.
        var sha256 = await blobStore.PutAsync(command.Content, null, command.ContentType, ct);

        // Tamanho vem do store porque o stream de entrada já foi consumido.
        await using var stored = await blobStore.OpenAsync(sha256, ct);
        var size = stored.Length;

        var file = new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = command.ProjectSlug, // null hoje; forward-compatible
            Path = path,
            Sha256 = sha256,
            SizeBytes = size,
            Side = command.Side,
            Optional = command.Optional,
            Origin = ModFileOrigin.ManualUpload
        };

        var replacedId = version.UpsertFile(file);
        if (replacedId is { } oldId)
            await repository.RemoveFileAsync(version.Id, oldId, ct);

        // A versão veio destacada (AsNoTracking); update reanexa o grafo e o
        // arquivo novo entra junto.
        await repository.UpdateVersionAsync(version, ct);


        // A versão veio destacada (AsNoTracking); update reanexa o grafo e o
        // arquivo novo entra junto.
        await repository.UpdateVersionAsync(version, ct);

        var dto = new ModpackFileDto
        {
            Path = file.Path,
            Sha256 = file.Sha256,
            SizeBytes = file.SizeBytes,
            Side = file.Side,
            Optional = file.Optional
        };

        return Result<ModpackFileDto>.Success(dto);
    }

    // Normaliza e valida o caminho relativo, rejeitando path traversal.
    private static string? NormalizePath(string raw)
    {
        var normalized = raw.Trim().Replace('\\', '/').TrimStart('/');

        if (normalized.Length is 0 || normalized.Contains(".."))
            return null;

        return normalized;
    }
}

public sealed record AddManualFileCommand(
    Guid ModpackVersionId,
    string Path,
    Stream Content,
    string ContentType,
    FileSide Side,
    bool Optional,
    string? ProjectSlug = null);