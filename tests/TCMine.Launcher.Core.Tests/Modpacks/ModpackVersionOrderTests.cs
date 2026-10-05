using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     Ordem pelo NÚMERO da versão: é ela que decide se entrar num servidor
///     atualiza a instância ou recusa, e descer de versão parte o mundo.
/// </summary>
public class ModpackVersionOrderTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.10.0", "1.9.0", 1)]          // numérico, não texto
    [InlineData("1.2", "1.2.0", 0)]
    [InlineData("1.2.0-alpha.1", "1.2.0", -1)]  // a estável vem depois do pré-lançamento
    [InlineData("1.2.0-alpha.2", "1.2.0-alpha.10", -1)]
    [InlineData("v2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0+build5", "1.0.0", 0)]
    public void Compara_por_semver(string a, string b, int expected) =>
        ModpackVersionOrder.Compare(a, b).ShouldBe(expected);

    [Fact]
    public void Numero_que_nao_e_versao_nao_tem_ordem() =>
        ModpackVersionOrder.Compare("primavera", "1.0.0").ShouldBeNull();

    [Theory]
    [InlineData("1.0.0", "1.1.0", JoinStep.Update)]     // servidor à frente
    [InlineData("1.1.0", "1.0.1", JoinStep.Refuse)]     // hotfix antigo publicado depois: é descer
    [InlineData("1.0.0", "1.0.0", JoinStep.Update)]     // mesmo número, outra publicação: alinha
    [InlineData("1.0.0", "inverno", JoinStep.Refuse)]   // sem como comparar: o lado seguro
    public void Entrar_atualiza_ou_recusa(string installed, string server, JoinStep expected) =>
        JoinServer.Decide(installed, server).ShouldBe(expected);
}
