using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Ponto único por onde uma importação entra na fila.
///     Grava o pedido ANTES de enfileirar, pelo mesmo motivo do IngestionScheduler
///     — só que aqui a janela é bem maior: a importação passa minutos baixando e
///     lendo o zip do pack antes de gravar a primeira linha. Uma queda nesse
///     trecho não deixava vestígio nenhum, nem para o admin nem para o arranque.
/// </summary>
public sealed class ImportScheduler(
    IImportRequestRepository requests,
    IImportQueue queue)
{
    /// <summary>Devolve o id do pedido, que é também o id do acompanhamento.</summary>
    public async Task<Result<Guid>> ScheduleAsync(
        ModFileOrigin origin, string projectId, string? fileId, string displayName, CancellationToken ct)
    {
        // Duas importações do mesmo pack em paralelo criariam dois modpacks
        // disputando a mesma procedência — a checagem que o caso de uso já faz
        // contra o catálogo, aplicada ao que ainda está em curso.
        if (await requests.ExistsForAsync(origin, projectId, ct))
            return Result<Guid>.Fail("Este pack já está sendo importado. Espere terminar.");

        var request = new ImportRequest
        {
            Origin = origin,
            ProjectId = projectId,
            FileId = fileId,
            DisplayName = displayName
        };

        await requests.AddAsync(request, ct);
        await queue.EnqueueAsync(request, ct);

        return Result<Guid>.Success(request.Id);
    }
}
