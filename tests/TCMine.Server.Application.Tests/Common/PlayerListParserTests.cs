using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Tests.Common;

/// <summary>
///     A saída do <c>list</c> não é contrato: é texto de log, e muda entre
///     vanilla, Paper e mods que reescrevem o comando. Os casos aqui são os
///     formatos que se sabe existirem, e o último teste é o que mais importa —
///     diante de algo irreconhecível, responder "não sei" em vez de "zero".
/// </summary>
public sealed class PlayerListParserTests
{
    [Theory]
    [InlineData("There are 3 of a max of 20 players online: ana, bia, caio", 3)]
    [InlineData("There are 0 of a max of 20 players online:", 0)]
    [InlineData("There are 12/40 players online: ...", 12)]
    [InlineData("there are 7 of a max of 20 players online", 7)]
    public void Le_a_contagem_dos_formatos_conhecidos(string saida, int esperado) =>
        PlayerListParser.Parse(saida).ShouldBe(esperado);

    [Fact]
    public void Prefixo_de_log_nao_atrapalha()
    {
        // O rcon-cli pode devolver a linha com o carimbo do servidor na frente.
        var saida = "[12:34:56] [Server thread/INFO]: There are 2 of a max of 20 players online: ana, bia";

        PlayerListParser.Parse(saida).ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Unknown command. Type \"/help\" for help.")]
    public void Saida_irreconhecivel_vira_nulo_e_nao_zero(string? saida)
    {
        // Zero significaria "servidor vazio". Afirmar isso sem saber colocaria
        // um número errado na tela de todo mundo, e um número errado é pior que
        // um traço.
        PlayerListParser.Parse(saida).ShouldBeNull();
    }
}
