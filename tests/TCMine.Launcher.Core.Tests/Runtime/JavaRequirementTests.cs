using TCMine.Launcher.Core.Runtime;

namespace TCMine.Launcher.Core.Tests.Runtime;

/// <summary>
///     Que Java cada versão do Minecraft exige.
///     Errar aqui não dá erro legível: o jogo abre e morre com um
///     <c>UnsupportedClassVersionError</c>, que fala de número de bytecode e não
///     de "instale o Java 21". O jogador reporta isso como "não abre".
/// </summary>
public class JavaRequirementTests
{
    [Theory]
    [InlineData("1.21", 21)]
    [InlineData("1.21.1", 21)]
    [InlineData("1.20.5", 21)]
    [InlineData("1.20.6", 21)]
    public void Versoes_modernas_pedem_java_21(string minecraft, int esperado) =>
        JavaRequirement.ForMinecraft(minecraft).ShouldBe(esperado);

    [Theory]
    [InlineData("1.20.4", 17)]
    [InlineData("1.20", 17)]
    [InlineData("1.18.2", 17)]
    [InlineData("1.17", 17)]
    public void Da_1_17_a_1_20_4_pedem_java_17(string minecraft, int esperado) =>
        JavaRequirement.ForMinecraft(minecraft).ShouldBe(esperado);

    [Theory]
    [InlineData("1.16.5")]
    [InlineData("1.12.2")]
    [InlineData("1.8.9")]
    public void Versoes_antigas_pedem_java_8(string minecraft) =>
        JavaRequirement.ForMinecraft(minecraft).ShouldBe(8);

    [Fact]
    public void A_fronteira_do_20_5_e_exata()
    {
        // O salto aconteceu DENTRO da 1.20, e não na virada da minor. Tratar
        // "1.20.x" como um bloco só erraria metade da série.
        JavaRequirement.ForMinecraft("1.20.4").ShouldBe(17);
        JavaRequirement.ForMinecraft("1.20.5").ShouldBe(21);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24w14a")]
    [InlineData("2.0")]
    public void Versao_ilegivel_assume_o_mais_novo(string? minecraft)
    {
        // Assumir o antigo faria um pack atual falhar de imediato; o contrário só
        // afeta packs que já ninguém publica.
        JavaRequirement.ForMinecraft(minecraft).ShouldBe(JavaRequirement.Newest);
    }

    [Theory]
    [InlineData("25.1")]
    [InlineData("26.2")]
    public void O_esquema_ano_release_vai_para_o_mais_novo(string minecraft)
    {
        // O Minecraft deixou "1.y.z" e passou a "ano.release". Tudo o que não é
        // "1.x" é posterior ao que a tabela cobre.
        // Este palpite já não é a fonte primária — quem responde é o
        // IJavaRequirementSource, lendo o javaVersion da própria versão —, mas
        // errar aqui voltaria a dar "Could not create the Java Virtual Machine"
        // em quem estivesse sem rede.
        JavaRequirement.ForMinecraft(minecraft).ShouldBe(JavaRequirement.Newest);
    }
}
