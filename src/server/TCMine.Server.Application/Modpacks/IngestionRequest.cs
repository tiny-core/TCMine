using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Descreve um mod a resolver durante a ingestão.
///     Origin diz de onde buscar; ProjectId e FileId identificam o mod naquela
///     origem. Path é onde o arquivo vai na instância — normalmente "mods/", mas
///     o modelo já suporta outros lugares.
/// </summary>
public sealed record ModIngestionItem(
    ModFileOrigin Origin,
    string ProjectId,
    string? FileId,
    FileSide Side);