using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     A porta de um servidor saía do texto do endereço, e dois servidores sem
///     ":porta" caíam ambos na 25565. Estas são as regras que substituem isso.
/// </summary>
public sealed class GamePortRangeTests
{
    [Fact]
    public void A_primeira_livre_e_a_menor_que_ninguem_usa()
    {
        var used = new HashSet<int> { 25565, 25566, 25568 };

        Assert.Equal(25567, GamePortRange.FirstFree(25565, 25599, used));
    }

    [Fact]
    public void Faixa_vazia_de_uso_devolve_o_inicio()
    {
        Assert.Equal(25565, GamePortRange.FirstFree(25565, 25599, new HashSet<int>()));
    }

    [Fact]
    public void Faixa_esgotada_devolve_nulo_em_vez_de_sair_dela()
    {
        var used = new HashSet<int> { 25565, 25566 };

        Assert.Null(GamePortRange.FirstFree(25565, 25566, used));
    }

    [Fact]
    public void As_duas_pontas_da_faixa_contam()
    {
        var used = new HashSet<int> { 25565 };

        Assert.Equal(25566, GamePortRange.FirstFree(25565, 25566, used));
    }

    [Theory]
    [InlineData(0, 0)] // a linha de configurações antes da migration
    [InlineData(25599, 25565)] // invertida
    [InlineData(80, 90)] // privilegiadas
    [InlineData(25565, 70000)] // além do limite
    public void Faixa_invalida_cai_na_padrao(int start, int end)
    {
        Assert.Equal((25565, 25599), GamePortRange.Normalize(start, end));
    }

    [Fact]
    public void Faixa_valida_e_mantida()
    {
        Assert.Equal((30000, 30010), GamePortRange.Normalize(30000, 30010));
    }

    [Theory]
    [InlineData(1023, false)]
    [InlineData(1024, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void Portas_privilegiadas_e_fora_do_limite_sao_recusadas(int port, bool valid)
    {
        Assert.Equal(valid, GamePortRange.IsValid(port));
    }

    [Theory]
    [InlineData("play.exemplo.com", 25565)] // sem porta, o cliente usa a padrão
    [InlineData("play.exemplo.com:25570", 25570)]
    [InlineData(" 94.63.98.255:25566 ", 25566)]
    [InlineData("", 25565)]
    [InlineData("play.exemplo.com:abc", 25565)]
    public void Porta_do_endereco_e_a_escrita_ou_a_padrao(string address, int expected)
    {
        Assert.Equal(expected, GamePortRange.AddressPort(address));
    }

    [Theory]
    [InlineData("play.exemplo.com", 25570, "play.exemplo.com:25570")]
    [InlineData("play.exemplo.com:25570", 25571, "play.exemplo.com:25571")]
    [InlineData("play.exemplo.com:25570", 25565, "play.exemplo.com")] // a padrão fica implícita
    [InlineData("", 25570, "")] // sem host não há o que completar
    public void Trocar_a_porta_do_endereco_preserva_o_host(string address, int port, string expected)
    {
        Assert.Equal(expected, GamePortRange.WithPort(address, port));
    }
}
