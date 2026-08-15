using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     Download dos snapshots de mundo.
///     Vai por HTTP, não pelo SignalR: um mundo compactado tem centenas de MB, e
///     o circuito manteria tudo em memória. Exige sessão — diferente dos blobs de
///     modpack, que o launcher baixa anonimamente por hash, um backup contém dados
///     dos jogadores.
/// </summary>
public static class WorldBackupEndpoints
{
    public static IEndpointRouteBuilder MapWorldBackups(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/servers/{serverId:guid}/backups/{backupId:guid}", async (
                Guid serverId,
                Guid backupId,
                IServerRepository servers,
                IWorldBackupStore store,
                CancellationToken ct) =>
            {
                var backup = await servers.GetBackupAsync(backupId, ct);

                // Confere o dono: sem isto, saber o Id de um backup bastaria para
                // baixá-lo por qualquer rota de servidor.
                if (backup is null || backup.GameServerId != serverId)
                    return Results.NotFound();

                var stream = await store.OpenAsync(serverId, backup.FileName, ct);
                if (stream is null)
                    return Results.NotFound();

                return Results.File(
                    stream,
                    "application/zip",
                    $"mundo-{serverId}-{backup.FileName}",
                    enableRangeProcessing: true);
            })
            .RequireAuthorization();

        return app;
    }
}
