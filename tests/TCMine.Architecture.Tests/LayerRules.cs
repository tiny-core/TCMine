using System.Reflection;
using NetArchTest.Rules;
using TCMine.Contracts;
// O xunit v3 tem um Xunit.TestResult próprio e o using global de Xunit faz
// ele vencer a resolução. O alias desfaz a ambiguidade.
using ArchResult = NetArchTest.Rules.TestResult;

namespace TCMine.Architecture.Tests;

/// <summary>
///     As regras de dependência entre camadas, verificadas automaticamente.
///     Cada teste aqui corresponde a uma decisão que tomamos de propósito. Se um
///     deles falhar, a pergunta certa é "por que essa dependência apareceu?" —
///     Não "como faço o teste passar?".
/// </summary>
public class LayerRules
{
    private static readonly Assembly Contracts =
        typeof(AssemblyMarker).Assembly;

    private static readonly Assembly Domain =
        typeof(Server.Domain.AssemblyMarker).Assembly;

    private static readonly Assembly Application =
        typeof(Server.Application.AssemblyMarker).Assembly;

    private static readonly Assembly LauncherCore =
        typeof(Launcher.Core.AssemblyMarker).Assembly;

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

    [Fact]
    public void Contracts_nao_depende_de_nada_do_projeto()
    {
        // Contracts é o wire format: precisa ser leve e estável. Qualquer
        // dependência aqui é herdada por server e launcher ao mesmo tempo.
        ShouldPass(Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOnAny(
                "TCMine.Server",
                "TCMine.Launcher",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MudBlazor")
            .GetResult());
    }

    [Fact]
    public void Domain_nao_conhece_infraestrutura()
    {
        // Se o Domain souber de EF Core, as regras de negócio passam a
        // depender de como os dados são gravados — e trocar de banco vira
        // uma reescrita.
        ShouldPass(Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "TCMine.Server.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult());
    }

    [Fact]
    public void Application_nao_conhece_infraestrutura()
    {
        // A Application declara portas; quem implementa é a Infrastructure.
        // A dependência aponta para dentro, nunca para fora.
        ShouldPass(Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "TCMine.Server.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Docker")
            .GetResult());
    }

    [Fact]
    public void Launcher_nunca_referencia_o_server()
    {
        // O launcher roda na máquina do jogador. Uma referência ao projeto do
        // servidor levaria junto entidades com RconSecret e strings de
        // conexão — e o que vai no binário do cliente é público.
        ShouldPass(Types.InAssembly(LauncherCore)
            .ShouldNot()
            .HaveDependencyOnAny(
                "TCMine.Server",
                "Microsoft.EntityFrameworkCore")
            .GetResult());
    }

    [Fact]
    public void Server_nunca_referencia_o_launcher()
    {
        ShouldPass(Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOn("TCMine.Launcher")
            .GetResult());

        ShouldPass(Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOn("TCMine.Launcher")
            .GetResult());
    }

    [Fact]
    public void Launcher_Core_e_portavel()
    {
        // Esta é a regra que preserva o trabalho de portar para Linux depois.
        // MSAL entra na lista porque a variante com broker é Windows-only:
        // autenticação fica atrás de porta, não referenciada direto.
        ShouldPass(Types.InAssembly(LauncherCore)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.Win32",
                "System.Windows",
                "Microsoft.Identity.Client",
                "CmlLib")
            .GetResult());
    }
}
