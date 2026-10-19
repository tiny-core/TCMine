using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
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
///     É também onde a whitelist é aplicada: a entrada em Running é o primeiro
///     instante em que o RCON responde (Running = passou na verificação de
///     saúde, ver <c>DockerServerOrchestrator.StatusOf</c>). Pelo mesmo motivo
///     é onde o servidor ganha a marca de que tem mundo.
/// </summary>
public static class ServerStatusTransitions
{
    /// <summary>Verdadeiro quando havia o que gravar.</summary>
    /// <param name="activity">Nulo para não registrar a queda (o reconciliador não registra).</param>
    /// <param name="whitelist">Nulo para não sincronizar ao entrar em Running.</param>
    public static async Task<bool> ApplyAsync(
        GameServer server,
        GameServerStatus real,
        IServerRepository servers,
        IActivityLogRepository? activity,
        IServerWhitelistSync? whitelist,
        CancellationToken ct)
    {
        // Antes da guarda de "nada mudou", de propósito: um servidor que já
        // estava Running quando esta regra entrou (o de produção, por exemplo)
        // nunca mais passaria por uma transição. A marca só é gravada uma vez,
        // então repetir a checagem a cada evento custa um if.
        await MarkWorldAsync(server, real, servers, ct);

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

        // A sincronização ficava só no StartGameServer, logo depois de subir o
        // container — quando a verificação de saúde ainda dá Iniciando. Ela
        // saía calada ("servidor não está no ar") e a whitelist nunca entrava:
        // convidado levava "not white-listed". Aqui ela roda quando o servidor
        // aceita jogadores, venha a mudança de onde vier (evento do Docker,
        // varredura do arranque ou tela). Engole as próprias falhas.
        if (real is GameServerStatus.Running && whitelist is not null)
            await whitelist.HandleAsync(server.Id, ct);

        return true;
    }

    /// <summary>
    ///     Running quer dizer que passou na verificação de saúde, e ela só passa
    ///     com o mundo carregado: é o primeiro instante em que se sabe que o
    ///     level.dat existe. Sem esta marca, <c>ChangeServerVersion</c> trata o
    ///     servidor como "sem mundo" e troca a versão sem backup.
    /// </summary>
    private static async Task MarkWorldAsync(
        GameServer server, GameServerStatus real, IServerRepository servers, CancellationToken ct)
    {
        if (real is not GameServerStatus.Running || server.HasWorld)
            return;

        var now = DateTimeOffset.UtcNow;
        await servers.MarkWorldInitializedAsync(server.Id, now, ct);

        // A cópia de quem chamou passa a concordar com o banco. Se outro já
        // tinha marcado, a data dele é a que ficou lá; aqui só importa o "tem".
        server.WorldInitializedAt ??= now;
    }
}
