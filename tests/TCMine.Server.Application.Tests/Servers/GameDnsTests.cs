using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     Os nomes de DNS dos servidores. O que sai daqui vira registro na zona do
///     admin, então um rótulo inválido tem de ser recusado antes de chegar lá.
/// </summary>
public sealed class GameDnsTests
{
    [Theory]
    [InlineData("sobrevivencia", true)]
    [InlineData("atm-10", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData("-atm", false)]
    [InlineData("atm-", false)]
    [InlineData("atm_10", false)]
    [InlineData("atm 10", false)]
    [InlineData("a.b", false)] // dois níveis
    [InlineData("ATM", false)] // a validação é sobre o rótulo já normalizado
    [InlineData("sobrevivência", false)]
    public void Rotulo_valido_e_so_letras_digitos_e_hifen(string label, bool valid)
    {
        Assert.Equal(valid, GameDns.IsValidLabel(label));
    }

    [Fact]
    public void Rotulo_tem_no_maximo_63_caracteres()
    {
        Assert.True(GameDns.IsValidLabel(new string('a', 63)));
        Assert.False(GameDns.IsValidLabel(new string('a', 64)));
    }

    [Theory]
    [InlineData("  Sobrevivencia ", "sobrevivencia")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalizar_tira_espacos_e_maiusculas(string? text, string? expected)
    {
        Assert.Equal(expected, GameDns.NormalizeLabel(text));
    }

    [Theory]
    [InlineData("ATM 10 Server", "atm-10-server")]
    [InlineData("test-server", "test-server")]
    [InlineData("  Sobrevivência!  ", "sobrevivencia")]
    [InlineData("--", "")]
    [InlineData(null, "")]
    public void Sugestao_sai_do_nome_do_servidor(string? name, string expected)
    {
        Assert.Equal(expected, GameDns.Suggest(name));
    }

    [Fact]
    public void Sugestao_e_sempre_um_rotulo_valido_ou_vazia()
    {
        var suggested = GameDns.Suggest(new string('x', 80) + " servidor");

        Assert.True(GameDns.IsValidLabel(suggested));
    }

    [Theory]
    [InlineData("exemplo.com", true)]
    [InlineData("jogos.exemplo.com", true)]
    [InlineData("exemplo", false)] // um rótulo só não é um domínio
    [InlineData("exemplo..com", false)]
    [InlineData("exemplo.com:25565", false)]
    [InlineData("https://exemplo.com", false)]
    public void Dominio_tem_dois_rotulos_validos_ou_mais(string domain, bool valid)
    {
        Assert.Equal(valid, GameDns.IsValidDomain(domain));
    }

    [Fact]
    public void Nome_do_servidor_e_subdominio_mais_dominio()
    {
        Assert.Equal("sobrevivencia.exemplo.com", GameDns.ServerHost(" Sobrevivencia ", "Exemplo.com"));
        Assert.Equal("_minecraft._tcp.sobrevivencia.exemplo.com", GameDns.SrvName("sobrevivencia.exemplo.com"));
    }

    [Theory]
    [InlineData(null, "exemplo.com")]
    [InlineData("sobrevivencia", null)]
    [InlineData("", "")]
    public void Sem_subdominio_ou_sem_dominio_nao_ha_nome(string? subdomain, string? domain)
    {
        Assert.Null(GameDns.ServerHost(subdomain, domain));
    }

    [Fact]
    public void A_marca_distingue_duas_instalacoes()
    {
        // Produção e desenvolvimento podem partilhar a zona; uma não pode apagar
        // os registros da outra.
        var a = GameDns.CommentPrefix(Guid.CreateVersion7());
        var b = GameDns.CommentPrefix(Guid.CreateVersion7());

        Assert.NotEqual(a, b);
        Assert.StartsWith("tcmine:", a);

        // Com o maior sufixo que a sincronização usa, cabe no limite de 100
        // caracteres dos comentários no plano Free da Cloudflare.
        Assert.True($"{a}srv:{Guid.CreateVersion7():N}".Length <= 100);
    }
}
