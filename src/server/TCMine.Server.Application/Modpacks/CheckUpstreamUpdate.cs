using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Pergunta à origem se o autor publicou release mais nova que a espelhada
///     pela versão indicada.
///     É uma consulta barata de propósito — só o id/rótulo da última release, sem
///     baixar o pack. A numeração do autor ("4.2") vive à parte da nossa
///     ("1.0.0"): é comparando o <c>UpstreamFileId</c> gravado com o de lá que se
///     sabe que há novidade, não comparando textos de versão.
/// </summary>
public sealed class CheckUpstreamUpdate(
    IEnumerable<IUpstreamPackSource> sources,
    IModpackRepository repository)
{
    public async Task<Result<UpstreamUpdateStatus>> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<UpstreamUpdateStatus>.Fail("Versão não encontrada.");

        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack?.UpstreamProvider is not { } origin || modpack.UpstreamProjectId is not { } projectId)
            return Result<UpstreamUpdateStatus>.Fail("Este modpack não veio de uma origem externa.");

        IUpstreamPackSource? source = null;
        foreach (var candidate in sources.Where(s => s.Origin == origin))
        {
            if (!await candidate.IsAvailableAsync(ct))
                continue;

            source = candidate;
            break;
        }

        if (source is null)
            return Result<UpstreamUpdateStatus>.Fail($"A origem {origin} não está configurada.");

        var latest = await source.GetLatestReleaseAsync(projectId, ct);
        if (latest is null)
            return Result<UpstreamUpdateStatus>.Fail("Não foi possível consultar a origem.");

        var hasUpdate = !string.Equals(latest.FileId, version.UpstreamFileId, StringComparison.Ordinal);

        return Result<UpstreamUpdateStatus>.Success(new UpstreamUpdateStatus(
            version.UpstreamVersionLabel,
            latest.Label,
            latest.FileId,
            hasUpdate,
            latest.PublishedAt));
    }
}

/// <summary>
///     Rótulo do autor aqui × lá fora. <paramref name="HasUpdate" /> compara os
///     ids da release, não os rótulos — autor que republica com o mesmo nome
///     ainda conta como novidade.
/// </summary>
public sealed record UpstreamUpdateStatus(
    string? CurrentLabel,
    string LatestLabel,
    string LatestFileId,
    bool HasUpdate,
    DateTimeOffset PublishedAt);
