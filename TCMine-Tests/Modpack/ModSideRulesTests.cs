using TCMine_Domain.Modpack;
using Xunit;

namespace TCMine_Tests.Modpack;

// Regra compartilhada servidor↔launcher: os dois lados TÊM de decidir igual quem vai para cada
// instância. Um bug aqui manda mod client-only para o servidor (ou omite um mod do servidor).
public class ModSideRulesTests
{
    [Theory]
    [InlineData(ModSide.Both, true)]
    [InlineData(ModSide.Client, true)]
    [InlineData(ModSide.Server, false)]
    public void RunsOnClient_cobre_both_e_client(ModSide side, bool expected)
    {
        Assert.Equal(expected, ModSideRules.RunsOnClient(side));
    }

    [Theory]
    [InlineData(ModSide.Both, true)]
    [InlineData(ModSide.Server, true)]
    [InlineData(ModSide.Client, false)]
    public void RunsOnServer_cobre_both_e_server(ModSide side, bool expected)
    {
        Assert.Equal(expected, ModSideRules.RunsOnServer(side));
    }

    [Fact]
    public void Both_roda_nos_dois_lados()
    {
        Assert.True(ModSideRules.RunsOnClient(ModSide.Both));
        Assert.True(ModSideRules.RunsOnServer(ModSide.Both));
    }

    [Fact]
    public void Client_e_Server_sao_mutuamente_exclusivos()
    {
        // Um mod Client não pode acabar no servidor, e vice-versa
        Assert.False(ModSideRules.RunsOnServer(ModSide.Client));
        Assert.False(ModSideRules.RunsOnClient(ModSide.Server));
    }

    [Fact]
    public void Both_e_o_default_do_enum()
    {
        // O comentário do domínio garante que Both == 0 (valor padrão); protege contra reordenação
        Assert.Equal(ModSide.Both, default);
    }
}
