using TCMine.Contracts.Servers;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Privilégios sobre um servidor que não passam pelo console.
///     Fica ao lado da <see cref="ConsoleCommandPolicy" /> e pela mesma razão: a
///     decisão de "quem pode o quê" mora num lugar só, testável sem HTTP. A UI
///     esconder o botão é conveniência; quem tem a URL chama o endpoint direto.
/// </summary>
public static class ServerAccessPolicy
{
    /// <summary>
    ///     Baixar ou restaurar um snapshot de mundo.
    ///     Exige Admin porque o .zip carrega dados dos jogadores — coordenadas de
    ///     base, inventário, o conteúdo de cada baú. Moderador modera a partida;
    ///     isso não lhe dá direito ao save inteiro numa máquina qualquer.
    /// </summary>
    public static bool CanAccessBackups(ServerRoleDto role) => role >= ServerRoleDto.Admin;
}
