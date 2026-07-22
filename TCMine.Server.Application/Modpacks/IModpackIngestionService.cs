using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Ingestão dos arquivos de uma versão de modpack.
///     Acontece UMA VEZ, no publish — nunca sob demanda por jogador. Mil
///     jogadores baixando o pack não geram nenhuma chamada à API do CurseForge,
///     porque a essa altura os arquivos já estão no seu disco.
///     Roda como job em background: resolver 200 mods leva minutos e não pode
///     ficar preso no request HTTP do admin.
/// </summary>
public interface IModpackIngestionService
{
    /// <summary>Enfileira a resolução e retorna na hora. Acompanhe pelo State.</summary>
    Task QueuePublishAsync(Guid modpackVersionId, CancellationToken ct);

    /// <summary>
    ///     Upload manual de um arquivo pelo admin.
    ///     Sempre disponível, e é o escape para os casos que nenhuma API resolve:
    ///     mod com redistribuição negada, build própria, config específica.
    /// </summary>
    Task<ModpackFileDto> AddManualFileAsync(
        Guid modpackVersionId,
        string path,
        Stream content,
        FileSide side,
        CancellationToken ct);
}