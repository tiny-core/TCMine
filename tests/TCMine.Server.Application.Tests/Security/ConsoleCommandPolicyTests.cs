using TCMine.Contracts.Servers;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Os testes aqui documentam decisões de segurança. Se algum deles começar a
///     falhar após uma refatoração, a pergunta certa não é "como faço passar"
///     e sim "eu acabei de abrir um buraco?".
/// </summary>
public class ConsoleCommandPolicyTests
{
    [Fact]
    public void Moderador_nao_pode_se_promover_a_operador()
    {
        // O ataque mais óbvio: se "op" passasse, qualquer moderador viraria
        // administrador do servidor de jogo em um comando.
        ConsoleCommandPolicy
            .IsAllowed(ServerRoleDto.Moderator, "op")
            .ShouldBeFalse();
    }

    [Fact]
    public void Moderador_nao_pode_derrubar_o_servidor()
    {
        ConsoleCommandPolicy
            .IsAllowed(ServerRoleDto.Moderator, "stop")
            .ShouldBeFalse();
    }

    [Fact]
    public void Comando_desconhecido_e_negado_por_padrao()
    {
        // Aqui está a diferença entre allowlist e blocklist: um mod pode
        // registrar qualquer comando novo, e a política nega sem precisar
        // conhecê-lo.
        ConsoleCommandPolicy
            .IsAllowed(ServerRoleDto.Moderator, "comando-de-mod-qualquer")
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData("kick")]
    [InlineData("ban")]
    [InlineData("whitelist")]
    [InlineData("tp")]
    public void Moderador_pode_usar_os_comandos_da_allowlist(string comando)
    {
        ConsoleCommandPolicy
            .IsAllowed(ServerRoleDto.Moderator, comando)
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData("KICK")]
    [InlineData("Kick")]
    [InlineData("kIcK")]
    public void A_verificacao_ignora_maiusculas_e_minusculas(string comando)
    {
        // Sem o StringComparer.OrdinalIgnoreCase no HashSet, "KICK" escaparia
        // da allowlist — e a mesma brecha valeria para "OP".
        ConsoleCommandPolicy
            .IsAllowed(ServerRoleDto.Moderator, comando)
            .ShouldBeTrue();
    }

    [Fact]
    public void Membro_comum_nao_executa_nenhum_comando()
    {
        ConsoleCommandPolicy
            .IsAllowed(ServerRoleDto.Member, "list")
            .ShouldBeFalse();
    }

    [Fact]
    public void Membro_comum_nao_le_o_console()
    {
        // Ler parece inofensivo, mas o log traz o IP de cada jogador que
        // entra e o chat inteiro da partida.
        ConsoleCommandPolicy
            .CanReadConsole(ServerRoleDto.Member)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(ServerRoleDto.Moderator)]
    [InlineData(ServerRoleDto.Admin)]
    [InlineData(ServerRoleDto.Owner)]
    public void Moderador_para_cima_le_o_console(ServerRoleDto papel) =>
        ConsoleCommandPolicy.CanReadConsole(papel).ShouldBeTrue();
}
