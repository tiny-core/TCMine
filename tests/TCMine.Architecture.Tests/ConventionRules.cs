using System.Reflection;
using NetArchTest.Rules;
using TCMine.Contracts;
// O xunit v3 tem um Xunit.TestResult próprio e o using global de Xunit faz
// ele vencer a resolução. O alias desfaz a ambiguidade.
using ArchResult = NetArchTest.Rules.TestResult;

namespace TCMine.Architecture.Tests;

public class ConventionRules
{
    private static readonly Assembly Contracts =
        typeof(AssemblyMarker).Assembly;

    private static readonly Assembly Domain =
        typeof(Server.Domain.AssemblyMarker).Assembly;

    [Fact]
    public void Dtos_sao_seladas()
    {
        // Herdar de DTO quebra a serialização de formas difíceis de
        // diagnosticar: o serializador escreve os campos do tipo declarado e
        // ignora silenciosamente o que a classe derivada acrescentou.
        ShouldPass(Types.InAssembly(Contracts)
            .That()
            .HaveNameEndingWith("Dto")
            .Should()
            .BeSealed()
            .GetResult());
    }

    [Fact]
    public void Entidades_de_dominio_sao_seladas()
    {
        ShouldPass(Types.InAssembly(Domain)
            .That()
            .ResideInNamespaceStartingWith("TCMine.Server.Domain")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .GetResult());
    }

    /// <summary>
    ///     Mensagem de falha com os tipos culpados. O padrão do NetArchTest só
    ///     diz que falhou, e aí você fica caçando qual classe foi.
    /// </summary>
    private static void ShouldPass(ArchResult resultado)
    {
        var culpados = resultado.FailingTypeNames is { } nomes
            ? string.Join(", ", nomes)
            : "(nenhum informado)";

        resultado.IsSuccessful.ShouldBeTrue($"Tipos violando a regra: {culpados}");
    }
}