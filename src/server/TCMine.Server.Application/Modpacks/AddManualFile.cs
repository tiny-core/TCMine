using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Anexa um arquivo enviado manualmente pelo admin a uma versão em Draft.
///     Só funciona em Draft: uma versão publicada é imutável, e alterar a lista
///     de arquivos dela quebraria a promessa de que um pack que funcionava
///     continua funcionando.
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

        // O blob store grava e devolve o hash real, calculado durante a
        // escrita. É a chave do arquivo daqui para frente.
        var sha256 = await blobStore.PutAsync(command.Content, null, command.ContentType, ct);

        // O tamanho vem de uma segunda consulta ao store em vez de contar aqui
        // porque o stream já foi consumido pela gravação. O store é a fonte
        // da verdade sobre o que foi gravado.
        await using var stored = await blobStore.OpenAsync(sha256, ct);
        var size = stored.Length;

        var file = new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = path,
            Sha256 = sha256,
            SizeBytes = size,
            Side = command.Side,
            Optional = command.Optional,
            Origin = ModFileOrigin.ManualUpload
        };

        version.Files.Add(file);
        await repository.SaveChangesAsync(ct);

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

    /// <summary>
    ///     Normaliza e valida o caminho relativo.
    ///     Rejeita path traversal: um caminho com ".." escaparia da pasta da
    ///     instância no cliente. Barra invertida vira barra normal para o mesmo
    ///     caminho funcionar nos dois sistemas.
    /// </summary>
    private static string? NormalizePath(string raw)
    {
        var normalized = raw.Trim().Replace('\\', '/').TrimStart('/');

        if (normalized.Length is 0)
            return null;

        if (normalized.Contains(".."))
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
    bool Optional);