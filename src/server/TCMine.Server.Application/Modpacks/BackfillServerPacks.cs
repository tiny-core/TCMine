using TCMine.Server.Domain.Modpacks;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Descobre o server pack das versões que foram importadas antes de o TCMine
///     saber que ele existia.
///     Sem isto, a saída pelo server pack só valeria para packs importados de
///     agora em diante — quem já tem um pack grande com uma dúzia de pendências,
///     que é justamente quem precisa, teria de reimportar tudo para ganhar um
///     botão.
///     Roda uma vez por versão: uma vez preenchido o campo, ela deixa de ser
///     candidata. Sem chave de API configurada nada acontece e a próxima
///     oportunidade tenta de novo — a informação não é urgente.
/// </summary>
public sealed class BackfillServerPacks(
    IEnumerable<IUpstreamPackSource> sources,
    IModpackRepository repository)
{
    /// <summary>Quantas versões passaram a ter server pack conhecido.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var modpacks = await repository.ListAsync(ct);
        var preenchidas = 0;

        foreach (var modpack in modpacks)
        {
            ct.ThrowIfCancellationRequested();

            if (modpack.UpstreamProvider is not { } provider
                || modpack.UpstreamProjectId is not { Length: > 0 } projectId)
            {
                continue;
            }

            var source = await OrigemAsync(provider, ct);
            if (source is null)
                continue;

            var versoes = await repository.ListVersionsAsync(modpack.Id, ct);

            foreach (var versao in versoes)
            {
                ct.ThrowIfCancellationRequested();

                // Já sabemos, ou não há como saber: os dois casos saem daqui.
                if (versao.UpstreamServerPackFileId is { Length: > 0 }
                    || versao.UpstreamFileId is not { Length: > 0 } fileId)
                {
                    continue;
                }

                var serverPack = await source.GetServerPackAsync(projectId, fileId, ct);
                if (serverPack is null)
                    continue;

                await repository.SetServerPackAsync(
                    versao.Id, serverPack.FileId, serverPack.PageUrl, ct);

                preenchidas++;
            }
        }

        return preenchidas;
    }

    private async Task<IUpstreamPackSource?> OrigemAsync(ModFileOrigin provider, CancellationToken ct)
    {
        foreach (var candidata in sources.Where(s => s.Origin == provider))
        {
            if (await candidata.IsAvailableAsync(ct))
                return candidata;
        }

        return null;
    }
}
