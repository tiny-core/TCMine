using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     O endereço que sai para o launcher e para o painel.
///     Nasceu de um defeito em produção: a 1.2.0 deu porta própria a cada
///     servidor, mas o endereço continuou a sair como o texto do formulário. Um
///     servidor na 25566 com o endereço "94.63.98.255" era entregue assim, e o
///     botão Entrar abria o jogo batendo na 25565 — a porta de OUTRO servidor.
/// </summary>
public sealed class GameAddressTests
{
    [Fact]
    public void Endereco_sem_porta_recebe_a_porta_do_servidor()
    {
        // A regressão, tal como aconteceu.
        Assert.Equal("94.63.98.255:25566", GameAddress.Resolve("94.63.98.255", 25566, null));
    }

    [Fact]
    public void Na_porta_padrao_o_endereco_sai_sem_porta()
    {
        // Como os jogadores o digitam: o cliente já assume a 25565.
        Assert.Equal("play.exemplo.com", GameAddress.Resolve("play.exemplo.com", 25565, null));
    }

    [Fact]
    public void Porta_escrita_no_endereco_vence_a_do_servidor()
    {
        // O roteador expõe a 25565 e entrega na 25570: quem sabe disso é o admin,
        // e ele o disse escrevendo a porta.
        Assert.Equal("play.exemplo.com:25565", GameAddress.Resolve(" play.exemplo.com:25565 ", 25570, "1.2.3.4"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Endereco_vazio_e_automatico_host_publico_mais_a_porta(string? address)
    {
        Assert.Equal("1.2.3.4:25566", GameAddress.Resolve(address, 25566, "1.2.3.4"));
        Assert.Equal("jogar.exemplo.com", GameAddress.Resolve(address, 25565, "jogar.exemplo.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Automatico_sem_host_publico_nao_tem_endereco(string? publicHost)
    {
        // Vazio, e não ":25566": quem recebe precisa conseguir distinguir "não há
        // endereço" de um endereço.
        Assert.Equal("", GameAddress.Resolve("", 25566, publicHost));
    }

    [Fact]
    public void Host_publico_nao_entra_quando_o_servidor_tem_endereco()
    {
        Assert.Equal("sobrevivencia.exemplo.com:25566",
            GameAddress.Resolve("sobrevivencia.exemplo.com", 25566, "1.2.3.4"));
    }

    [Fact]
    public void Servidor_com_nome_de_dns_sai_sem_porta()
    {
        // A porta vai no registro SRV; escrita no endereço, o jogo deixaria de
        // consultá-lo.
        Assert.Equal("sobrevivencia.exemplo.com",
            GameAddress.Resolve("", 25570, "1.2.3.4", "sobrevivencia.exemplo.com"));
    }

    [Fact]
    public void Endereco_escrito_vence_o_nome_de_dns()
    {
        Assert.Equal("outro.exemplo.com:25570",
            GameAddress.Resolve("outro.exemplo.com", 25570, "1.2.3.4", "sobrevivencia.exemplo.com"));
    }

    [Fact]
    public void Ipv6_so_leva_porta_entre_colchetes()
    {
        Assert.Equal("[2001:db8::1]:25566", GameAddress.Resolve("", 25566, "2001:db8::1"));
        Assert.Equal("[2001:db8::1]:25566", GameAddress.Resolve("2001:db8::1", 25566, null));
        Assert.Equal("2001:db8::1", GameAddress.Resolve("2001:db8::1", 25565, null));
    }

    [Theory]
    [InlineData("play.exemplo.com", "play.exemplo.com", null)]
    [InlineData("play.exemplo.com:25570", "play.exemplo.com", 25570)]
    [InlineData(" 94.63.98.255:25566 ", "94.63.98.255", 25566)]
    [InlineData("", "", null)]
    [InlineData("play.exemplo.com:abc", "play.exemplo.com:abc", null)] // não é porta: fica no host
    [InlineData("play.exemplo.com:70000", "play.exemplo.com:70000", null)] // além do limite
    [InlineData("2001:db8::1", "2001:db8::1", null)] // IPv6 sem colchetes não tem porta
    [InlineData("[2001:db8::1]", "2001:db8::1", null)]
    [InlineData("[2001:db8::1]:25570", "2001:db8::1", 25570)]
    public void Separa_host_e_porta(string address, string host, int? port)
    {
        Assert.Equal((host, port), GameAddress.Split(address));
    }

    [Theory]
    [InlineData("play.exemplo.com", 25570, "play.exemplo.com:25570")]
    [InlineData("play.exemplo.com:25570", 25571, "play.exemplo.com:25571")]
    [InlineData("play.exemplo.com:25570", 25565, "play.exemplo.com")] // a padrão fica implícita
    [InlineData("", 25570, "")] // sem host não há o que completar
    public void Trocar_a_porta_do_endereco_preserva_o_host(string address, int port, string expected)
    {
        Assert.Equal(expected, GameAddress.WithPort(address, port));
    }

    [Theory]
    [InlineData("jogar.exemplo.com", true)]
    [InlineData("94.63.98.255", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("", false)]
    [InlineData("jogar.exemplo.com:25565", false)] // a porta é a de cada servidor
    [InlineData("https://jogar.exemplo.com", false)]
    [InlineData("jogar.exemplo.com/mapa", false)]
    [InlineData("jogar exemplo.com", false)]
    [InlineData("[2001:db8::1]", false)]
    public void Endereco_publico_aceita_so_o_host(string text, bool valid)
    {
        Assert.Equal(valid, GameAddress.IsBareHost(text));
    }
}
