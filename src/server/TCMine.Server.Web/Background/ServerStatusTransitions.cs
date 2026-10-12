using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Grava a mudança de status de um servidor, e só ela.
///     Um lugar só para três chamadores que faziam isto cada um à sua maneira —
///     o reconciliador do arranque, o acompanhamento dos eventos do Docker e as
///     telas de servidores —, e que agora concordam no registro de "caiu".
///     Grava SÓ a coluna do status (<see cref="IServerRepository.UpdateStatusAsync" />),
///     nunca a linha inteira: quem chama leu o servidor um instante antes, e
///     gravar a cópia toda por cima desfaria o que outra pessoa salvou nesse
///     meio tempo — o admin mudando a memória enquanto o servidor caía.
/// </summary>
public static class ServerStatusTransitions
{
    /// <summary>Verdadeiro quando havia o que gravar.</summary>
    /// <param name="activity">Nulo para não registrar a queda (o reconciliador não registra).</param>
    public static async Task<bool> ApplyAsync(
        GameServer server,
        GameServerStatus real,
        IServerRepository servers,
        IActivityLogRepository? activity,
        CancellationToken ct)
    {
        if (server.Status == real)
            return false;

        server.Status = real;
        await servers.UpdateStatusAsync(server.Id, real, ct);

        // Só na TRANSIÇÃO: a guarda acima já evita repetir isto enquanto o
        // servidor continua caído. Com os eventos do Docker, "caiu" aqui passa
        // a ser o instante da queda, e não o momento em que alguém percebeu.
        if (real is GameServerStatus.Crashed && activity is not null)
        {
            await activity.AddAsync(
                new ActivityEvent
                {
                    Kind = ActivityEventKind.ServerCrashed,
                    Message = $"{server.Name} caiu.",
                    Href = "/admin/servers"
                }, ct);
        }

        return true;
    }
}
