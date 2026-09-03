using TCMine.Launcher.UI.Layout;

namespace TCMine.Launcher.Core.Tests.UI;

/// <summary>
///     Qual item do trilho acende.
///     Parece cosmético e não é: o item aceso é a única pista que o jogador tem
///     de onde está, e as regras de raiz e de prefixo se contradizem se
///     aplicadas sem cuidado.
/// </summary>
public sealed class NavMatchTests
{
    [Fact]
    public void Raiz_acende_apenas_na_raiz()
    {
        NavMatch.IsActive("/", "").ShouldBeTrue();
        NavMatch.IsActive("/", "modpacks").ShouldBeFalse();
    }

    [Fact]
    public void Item_acende_na_propria_rota()
    {
        NavMatch.IsActive("/modpacks", "modpacks").ShouldBeTrue();
    }

    [Fact]
    public void Item_continua_aceso_numa_rota_filha()
    {
        // O detalhe de "/modpacks/{id}" ainda ser a secção Modpacks.
        NavMatch.IsActive("/modpacks", "modpacks/018f2c/versions/3").ShouldBeTrue();
    }

    [Fact]
    public void Prefixo_nao_atravessa_a_fronteira_de_segmento()
    {
        // A regressão que motivou extrair esta função: um StartsWith solto
        // acendia "/mods" ao abrir "/modspack", porque as letras coincidem.
        NavMatch.IsActive("/mods", "modspack").ShouldBeFalse();
        NavMatch.IsActive("/news", "newsletter").ShouldBeFalse();
    }

    [Fact]
    public void Query_e_barra_final_nao_mudam_o_resultado()
    {
        NavMatch.IsActive("/modpacks", "modpacks/?busca=jei").ShouldBeTrue();
        NavMatch.IsActive("/", "?tab=1").ShouldBeTrue();
    }
}
