using TCMine.Contracts.Servers;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Quem pode rodar o quê no console.
///     ALLOWLIST, nunca blocklist. Console de Minecraft é execução arbitrária
///     por design: "op" se dá administrador, "stop" derruba todos, e mods
///     adicionam comandos que você nem sabe que existem. Tentar listar o que é
///     proibido é uma corrida que você perde — liste o que é permitido.
/// </summary>
public static class ConsoleCommandPolicy
{
    private static readonly HashSet<string> ComandosDeModerador =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "kick",
            "ban",
            "pardon",
            "mute",
            "tp",
            "whitelist",
            "list",
            "say",
            "msg"
        };

    public static bool IsAllowed(ServerRoleDto role, string command)
    {
        return role switch
        {
            ServerRoleDto.Owner or ServerRoleDto.Admin => true,
            ServerRoleDto.Moderator => ComandosDeModerador.Contains(command),
            _ => false
        };
    }

    /// <summary>
    ///     Ler o console também é privilégio.
    ///     O log traz o IP de cada jogador que entra e o chat inteiro da partida.
    ///     Não é tela de "membro", por mais inofensivo que "só ler" pareça.
    /// </summary>
    public static bool CanReadConsole(ServerRoleDto role) => role >= ServerRoleDto.Moderator;
}
