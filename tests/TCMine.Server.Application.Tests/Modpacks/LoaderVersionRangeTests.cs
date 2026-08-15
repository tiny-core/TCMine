using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     O comparador de versão de loader tem um viés deliberado: na dúvida,
///     aceita. Uma recusa errada bloqueia um mod que funcionaria e o admin não
///     tem como contornar; um aceite errado só devolve o comportamento anterior
///     à checagem. Metade destes testes existe para travar esse viés.
/// </summary>
public sealed class LoaderVersionRangeTests
{
    [Theory]
    // Maven, do Forge/NeoForge.
    [InlineData("[21.1.80,)", "21.1.100", true)]
    [InlineData("[21.1.80,)", "21.1.80", true)]
    [InlineData("[21.1.80,)", "21.1.79", false)]
    [InlineData("[21.1.80,)", "20.4.100", false)]
    [InlineData("(21.1.80,)", "21.1.80", false)]
    [InlineData("[1.0,2.0)", "1.5", true)]
    [InlineData("[1.0,2.0)", "2.0", false)]
    [InlineData("[1.0,2.0]", "2.0", true)]
    [InlineData("[21.1.80]", "21.1.80", true)]
    [InlineData("[21.1.80]", "21.1.81", false)]
    // Fabric.
    [InlineData(">=0.15.0", "0.16.9", true)]
    [InlineData(">=0.15.0", "0.14.9", false)]
    [InlineData(">0.15.0", "0.15.0", false)]
    public void Compara_intervalos(string exigido, string atual, bool esperado) =>
        Assert.Equal(esperado, LoaderVersionRange.IsSatisfied(exigido, atual));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("1.2.x")]
    [InlineData("qualquer-coisa")]
    // Versão solta no Forge é preferência, não exigência: tratá-la como mínimo
    // recusaria mods que funcionam perfeitamente.
    [InlineData("21.1.999")]
    public void Na_duvida_aceita(string? exigido) =>
        Assert.True(LoaderVersionRange.IsSatisfied(exigido, "21.1.100"));

    [Fact]
    public void Versao_do_loader_desconhecida_aceita()
    {
        // Sem saber contra o que comparar, recusar seria chutar.
        Assert.True(LoaderVersionRange.IsSatisfied("[21.1.80,)", null));
        Assert.True(LoaderVersionRange.IsSatisfied("[21.1.80,)", "neoforge-latest"));
    }

    [Fact]
    public void Segmentos_faltando_contam_como_zero()
    {
        // "21.1" contra "[21.1.0,)" é a mesma coisa.
        Assert.True(LoaderVersionRange.IsSatisfied("[21.1.0,)", "21.1"));
        Assert.False(LoaderVersionRange.IsSatisfied("[21.1.1,)", "21.1"));
    }

    [Fact]
    public void Sufixo_de_pre_lancamento_e_ignorado()
    {
        Assert.True(LoaderVersionRange.IsSatisfied("[21.1.80,)", "21.1.100-beta"));
    }
}
