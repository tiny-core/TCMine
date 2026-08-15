using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;

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
        app.MapGet("/api/v1/servers/{serverId:guid}/backups/{backupId:guid}", DownloadAsync)
            // RequireAuthorization garante apenas que HÁ sessão. Quem pode baixar
            // O QUÊ é decidido dentro do handler, contra o papel neste servidor.
            .RequireAuthorization();

        return app;
    }

    /// <summary>
    ///     Handler nomeado em vez de lambda para poder ser testado sem subir o
    ///     servidor — a autorização daqui é justamente o que não pode regredir.
    /// </summary>
    internal static async Task<IResult> DownloadAsync(
        Guid serverId,
        Guid backupId,
        ICurrentUserScope scope,
        IServerRepository servers,
        IWorldBackupStore store,
        CancellationToken ct)
    {
        // Estar autenticado não basta: sem esta checagem, qualquer usuário do
        // painel baixava o mundo de qualquer servidor sabendo os dois GUIDs.
        var role = await scope.GetRoleAsync(serverId, ct);
        if (role is null || !ServerAccessPolicy.CanAccessBackups(role.Value))
            return Results.NotFound();

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
    }
}
