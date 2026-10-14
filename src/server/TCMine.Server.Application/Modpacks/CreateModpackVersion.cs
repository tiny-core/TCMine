using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Cria uma versão. Nasce em Draft, vazia; os arquivos vêm depois.
/// </summary>
public sealed class CreateModpackVersion(IModpackRepository repository, LoaderVersionGuard loaderGuard)
{
    public async Task<Result<Guid>> HandleAsync(CreateModpackVersionCommand command, CancellationToken ct)
    {
        var modpack = await repository.GetByIdAsync(command.ModpackId, ct);

        if (modpack is null)
            return Result<Guid>.Fail("Modpack não encontrado.");

        // Antes de qualquer outra regra: uma build que não existe para o
        // Minecraft do pack não vira versão, nem em rascunho.
        var loaderCheck = await loaderGuard.CheckAsync(
            modpack.Loader, modpack.MinecraftVersion, command.LoaderVersion, ct);
        if (!loaderCheck.Succeeded)
            return Result<Guid>.Fail(loaderCheck.Error!);

        var versionText = command.Version.Trim();

        // Um Draft de cada vez: força terminar e publicar antes de começar a
        // próxima. Evita duas versões meio-feitas em paralelo.
        var versions = await repository.ListVersionSummariesAsync(modpack.Id, ct);
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

        if (command.InheritFiles)
        {
            // Herda mods + overrides de uma versão publicada: a que o admin
            // escolheu ou, sem escolha, a mais recente. O admin depois poda na
            // grade o que já não serve — a ausência na nova versão faz o
            // launcher apagar o arquivo no update (diff declarativo).
            var published = modpack.Versions
                .Where(v => v.State is ModpackVersionState.Ready or ModpackVersionState.Archived)
                .ToList();

            Guid? sourceId;
            if (command.SourceVersionId is { } chosen)
            {
                // Rascunho ou falha como origem copiaria o que nunca foi
                // publicado; versão de outro pack, mods de outro jogo.
                if (published.All(v => v.Id != chosen))
                    return Result<Guid>.Fail("A versão de origem tem de ser uma versão publicada deste modpack.");

                sourceId = chosen;
            }
            else
            {
                sourceId = published
                    .Where(v => v.State is ModpackVersionState.Ready)
                    .OrderByDescending(v => v.Id) // GUID v7 = mais recente primeiro
                    .Select(v => (Guid?)v.Id)
                    .FirstOrDefault();
            }

            if (sourceId is { } id)
            {
                // GetVersionAsync inclui os Files; modpack.Versions não os traz.
                var source = await repository.GetVersionAsync(id, ct);
                if (source is not null)
                {
                    foreach (var f in source.Files)
                        version.UpsertFile(f.CopyTo(version.Id));

                    // Mesmos arquivos, mesma base do autor: sem isto a versão
                    // nova esquecia de que pack veio.
                    version.InheritUpstreamFrom(source);
                }
            }
        }

        await repository.AddVersionAsync(version, ct);
        return Result<Guid>.Success(version.Id);
    }
}

public sealed record CreateModpackVersionCommand(
    Guid ModpackId,
    string Version,
    string LoaderVersion,
    int? RecommendedMemoryMb,
    bool InheritFiles,
    Guid? SourceVersionId = null);
