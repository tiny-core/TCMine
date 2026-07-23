using TCMine.Contracts.Modpacks;

namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     O que precisa acontecer para a instância ficar igual ao manifest.
///     Separar "baixar" de "materializar" é o que permite instalar um pack novo
///     quase instantaneamente quando ele compartilha mods com outro já instalado:
///     os arquivos já estão no store, basta criar os hardlinks.
/// </summary>
public sealed record SyncPlan
{
    public required InstanceKey Instance { get; init; }

    /// <summary>Arquivos que faltam no content store e precisam vir da rede.</summary>
    public required IReadOnlyList<ModpackFileDto> ToDownload { get; init; }

    /// <summary>Arquivos que precisam aparecer na pasta da instância.</summary>
    public required IReadOnlyList<ModpackFileDto> ToMaterialize { get; init; }

    /// <summary>Caminhos que sobraram de uma versão anterior.</summary>
    public required IReadOnlyList<string> ToDelete { get; init; }

    public long BytesToDownload => ToDownload.Sum(f => f.SizeBytes);

    public bool IsUpToDate =>
        ToDownload.Count is 0 && ToMaterialize.Count is 0 && ToDelete.Count is 0;
}