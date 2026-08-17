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

    /// <summary>
    ///     Ligar e desligar o servidor. Admin porque derrubar a partida atinge
    ///     todo mundo que está jogando — é operação de dono da máquina, não de
    ///     quem modera o chat.
    /// </summary>
    public static bool CanControlPower(ServerRoleDto role) => role >= ServerRoleDto.Admin;

    /// <summary>
    ///     Mexer na configuração: nome, endereço, RAM, limite de jogadores e a
    ///     versão do modpack. Trocar a versão reescreve os mods da instância, e
    ///     por isso pesa o mesmo que desligar.
    /// </summary>
    public static bool CanConfigure(ServerRoleDto role) => role >= ServerRoleDto.Admin;

    /// <summary>
    ///     Apagar o servidor. Só Owner: é a única ação da lista que não tem
    ///     volta — leva junto o mundo, e nenhum backup automático a precede.
    /// </summary>
    public static bool CanDelete(ServerRoleDto role) => role >= ServerRoleDto.Owner;

    /// <summary>
    ///     Convidar, remover e mudar o papel de alguém. Owner porque quem
    ///     gerencia membros pode promover a si mesmo — conceder isso a Admin
    ///     tornaria a distinção entre os dois papéis decorativa.
    /// </summary>
    public static bool CanManageMembers(ServerRoleDto role) => role >= ServerRoleDto.Owner;
}
