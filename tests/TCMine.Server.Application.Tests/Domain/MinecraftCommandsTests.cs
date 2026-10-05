using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Domain;

/// <summary>
///     A referência de comandos do console. Ela só ajuda a digitar — quem decide
///     o que passa é o SendServerCommand —, mas um nome que ele recusaria seria
///     um item da lista que nunca funciona.
/// </summary>
public sealed class MinecraftCommandsTests
{
    [Fact]
    public void Todo_comando_tem_a_forma_que_o_console_aceita() =>
        MinecraftCommands.All
            .Where(c => c.Name.Length is 0 or > 32
                        || !c.Name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'))
            .Select(c => c.Name)
            .ShouldBeEmpty();

    [Fact]
    public void Nomes_sao_unicos_e_a_sintaxe_comeca_pelo_nome()
    {
        MinecraftCommands.All.Select(c => c.Name).ShouldBeUnique();
        MinecraftCommands.All
            .Where(c => !c.Syntax.StartsWith(c.Name, StringComparison.Ordinal))
            .Select(c => c.Name)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Busca_pelo_nome_ignora_maiusculas() =>
        MinecraftCommands.Find("GameMode")!.Name.ShouldBe("gamemode");
}
