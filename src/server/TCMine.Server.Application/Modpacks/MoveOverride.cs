using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Move um override (ou uma pasta inteira) para outro caminho. Não toca no blob
///     — o conteúdo é content-addressed; mover é só mudar o Path e a identidade
///     sintética (override:{path}). Registra o path anterior para o undo.
/// </summary>
public sealed class MoveOverride(IModpackRepository repository, OverrideUndoService undo)
{
    public async Task<Result> HandleAsync(Guid versionId, string fromPath, string toPath, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível mover overrides em rascunho.");

        var from = Normalize(fromPath);
        var to = Normalize(toPath);
        if (from == to)
            return Result.Success();

        // Alvos: o próprio arquivo, OU todos os overrides sob a pasta "from/".
        var targets = version.Files
            .Where(f => f.Origin == ModFileOrigin.Override
                        && (PathEquals(f.Path, from) || IsUnder(f.Path, from)))
            .ToList();

        if (targets.Count == 0)
            return Result.Fail("Nada a mover neste caminho.");

        foreach (var file in targets)
        {
            // Remapeia mantendo a estrutura relativa: se movo a pasta "config"
            // para "backup/config", "config/a/b.toml" vira "backup/config/a/b.toml".
            var newPath = PathEquals(file.Path, from)
                ? to
                : $"{to}/{file.Path[(from.Length + 1)..]}";

            // Colisão: já existe um override no destino? Recusa — sobrescrever
            // em silêncio perderia dados.
            if (version.Files.Any(f => f.Id != file.Id
                                       && f.Origin == ModFileOrigin.Override
                                       && PathEquals(f.Path, newPath)))
                return Result.Fail($"Já existe um arquivo em '{newPath}'.");

            undo.Record(versionId, file.Id, file.Path); // para desfazer depois

            file.Path = newPath;
            file.ProjectSlug = $"override:{newPath}"; // identidade acompanha o path
        }

        await repository.UpdateVersionAsync(version, ct);
        return Result.Success();
    }

    private static string Normalize(string p)
    {
        return p.Replace('\\', '/').Trim('/');
    }

    private static bool PathEquals(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnder(string path, string folder)
    {
        return path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
    }
}