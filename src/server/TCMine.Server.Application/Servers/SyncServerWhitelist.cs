using Microsoft.Extensions.Logging;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Põe a whitelist do servidor de jogo de acordo com quem tem convite aqui.
///     Existe porque o Minecraft não tem senha de entrada: a lista de servidores
///     do cliente guarda endereço e nome, e o protocolo não prevê credencial
///     nenhuma. O mecanismo que o jogo oferece para "só quem eu deixar" é a
///     whitelist, e o TCMine já sabe quem são — o login do jogador é por perfil
///     Minecraft verificado, então temos o nome real de cada membro.
///     Vale registrar o limite: a whitelist prende à CONTA, não ao launcher. Um
///     convidado com os mods na mão poderia entrar por fora. Com um pack de
///     centenas de mods isso é teórico, mas não é a mesma promessa.
///     Roda por RCON, e não por variável de ambiente da imagem: comando de jogo
///     é estável entre versões do container, e ainda funciona com o servidor no
///     ar — trocar ambiente exigiria recriar o container e derrubar quem estava
///     jogando.
/// </summary>
public sealed partial class SyncServerWhitelist(
    IServerRepository servers,
    IMembershipRepository memberships,
    IRconClient rcon,
    ILogger<SyncServerWhitelist> logger) : IServerWhitelistSync
{
    private readonly ILogger<SyncServerWhitelist> _logger = logger;

    /// <summary>
    ///     Aplica a lista ao servidor. Silencioso quando ele está parado — a
    ///     próxima subida sincroniza, e não há o que fazer sem RCON.
    /// </summary>
    public async Task HandleAsync(Guid gameServerId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(gameServerId, ct);
        if (server is null || server.Status is not GameServerStatus.Running)
            return;

        try
        {
            if (!server.WhitelistEnabled)
            {
                // Desligar é uma linha e não precisa da lista: quem já estava
                // dentro continua, e a porta fica aberta.
                await rcon.ExecuteAsync(gameServerId, "whitelist off", ct);
                return;
            }

            await rcon.ExecuteAsync(gameServerId, "whitelist on", ct);

            var membros = await memberships.ListWithUsersAsync(gameServerId, ct);

            foreach (var membro in membros)
            {
                ct.ThrowIfCancellationRequested();

                // Sem perfil Minecraft não há o que adicionar: é alguém que criou
                // conta no painel e ainda não entrou no jogo. Some sozinho no
                // primeiro login, quando o UUID chega.
                if (membro.DisplayName is not { Length: > 0 } nome || membro.MinecraftUuid is null)
                    continue;

                await rcon.ExecuteAsync(gameServerId, $"whitelist add {nome}", ct);
            }

            // Relê o arquivo: sem isto, uma entrada escrita fora do jogo não
            // passa a valer até o próximo restart.
            await rcon.ExecuteAsync(gameServerId, "whitelist reload", ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o que chamou — resgatar um convite
            // vale mesmo que a whitelist não tenha entrado ainda, e a próxima
            // subida do servidor refaz. Mas fica no log, porque um convite que
            // não deixa entrar parece bug do convite.
            LogFalha(ex, gameServerId);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao sincronizar a whitelist do servidor {ServerId}. "
                  + "Quem tem convite pode não conseguir entrar até a próxima subida.")]
    private partial void LogFalha(Exception ex, Guid serverId);
}

/// <summary>
///     Porta para quem precisa disparar a sincronização — resgatar um convite,
///     remover um membro, subir o servidor.
///     Existe para que esses casos de uso não dependam da classe concreta: um
///     teste de convite não deve precisar montar RCON e repositório de servidor
///     para verificar que o convite foi resgatado.
/// </summary>
public interface IServerWhitelistSync
{
    Task HandleAsync(Guid gameServerId, CancellationToken ct);
}
