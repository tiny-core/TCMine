using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

public sealed class SaveOverride(IModpackRepository repository, IBlobStore blobStore)
{
    public async Task<Result> HandleAsync(Guid versionId, string path, string content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result.Fail("Informe o caminho do arquivo.");

        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível editar overrides em rascunho.");

        var normalized = path.Trim().Replace('\\', '/').TrimStart('/');

        // Grava o texto como blob. Content-addressed: se o conteúdo não mudou,
        // o SHA é o mesmo e o store deduplica sozinho.
        var bytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(bytes);
        var sha256 = await blobStore.PutAsync(stream, null, "text/plain; charset=utf-8", ct);

        var file = new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = normalized,
            Sha256 = sha256,
            SizeBytes = bytes.Length,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Override,
            // Para overrides o caminho É a identidade. Um slug sintético faz o
            // UpsertFile substituir a versão anterior do mesmo arquivo em vez de
            // acumular duas linhas no mesmo path — reusa a lógica de replace.
            ProjectSlug = $"override:{normalized}"
        };

        try
        {
            var replacedId = version.UpsertFile(file);
            if (replacedId is { } oldId)
                await repository.RemoveFileAsync(version.Id, oldId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        await repository.UpdateVersionAsync(version, ct);
        return Result.Success();
    }
}