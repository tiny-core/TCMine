using System.Reflection;
using NetArchTest.Rules;
// O xunit v3 tem um Xunit.TestResult próprio e o using global de Xunit faz
// ele vencer a resolução. O alias desfaz a ambiguidade.
using ArchResult = NetArchTest.Rules.TestResult;

namespace TCMine.Launcher.Architecture.Tests;

/// <summary>
///     As regras de dependência do launcher, verificadas automaticamente. Vive
///     num projeto à parte de TCMine.Architecture.Tests porque só compila junto
///     com o launcher, e o launcher não faz mais parte do CI — roda na IDE.
///     Cada teste aqui corresponde a uma decisão que tomamos de propósito. Se um
///     deles falhar, a pergunta certa é "por que essa dependência apareceu?" —
///     Não "como faço o teste passar?".
/// </summary>
public class LayerRules
{
    private static readonly Assembly LauncherCore =
        typeof(Launcher.Core.AssemblyMarker).Assembly;

    private static readonly Assembly LauncherUi =
        typeof(Launcher.UI.AssemblyMarker).Assembly;

    private static readonly Assembly LauncherInfrastructure =
        typeof(Launcher.Infrastructure.LauncherPaths).Assembly;

    /// <summary>
    ///     Mensagem de falha com os tipos culpados. O padrão do NetArchTest só
    ///     diz que falhou, e aí você fica caçando qual classe foi.
    /// </summary>
    private static void ShouldPass(ArchResult result)
    {
        var culpados = result.FailingTypeNames is { } names
            ? string.Join(", ", names)
            : "(nenhum informado)";

        result.IsSuccessful.ShouldBeTrue($"Tipos violando a regra: {culpados}");
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

    [Fact]
    public void Launcher_UI_e_portavel()
    {
        // As telas do launcher são uma RCL comum, e é essa a aposta: no dia de
        // rodar em Linux, portar significa escrever um host novo — as páginas
        // ficam intactas. Um único using de System.Windows aqui transformaria
        // esse trabalho numa reescrita.
        //
        // O que a tela precisa do sistema operacional (arrastar a janela,
        // minimizar) entra por IWindowChrome, implementada no host.
        ShouldPass(Types.InAssembly(LauncherUi)
            .ShouldNot()
            .HaveDependencyOnAny(
                "System.Windows",
                "Microsoft.Win32",
                "Microsoft.AspNetCore.Components.WebView",
                "TCMine.Server")
            .GetResult());
    }

    [Fact]
    public void Launcher_UI_nao_conhece_a_infraestrutura()
    {
        // Mesma direção de dependência do painel: a tela fala com portas, e quem
        // as implementa é montado pelo host. Sem isto, uma página passaria a
        // chamar HttpClient direto e a lógica de sincronização vazaria para
        // dentro de um componente Razor, onde nenhum teste alcança.
        ShouldPass(Types.InAssembly(LauncherUi)
            .ShouldNot()
            .HaveDependencyOn("TCMine.Launcher.Infrastructure")
            .GetResult());
    }

    [Fact]
    public void Launcher_Infrastructure_e_portavel()
    {
        // A infraestrutura do launcher — HTTP, SignalR, content store, manifesto
        // de instância, motor do jogo — é lógica que roda igual em qualquer
        // sistema. O que é do Windows vive em TCMine.Launcher.Infrastructure.Windows
        // e entra por porta: o hardlink e o MSAL.
        //
        // Esta regra é o que impede o atalho: uma chamada de P/Invoke "só desta
        // vez" aqui obrigaria a reescrever o projeto inteiro no dia do port, em
        // vez de escrever um Infrastructure.Linux ao lado.
        //
        // O CmlLib esteve nesta lista e SAIU, porque estar aqui era um erro de
        // agrupamento: ele é multiplataforma, ao contrário do Microsoft.Win32, do
        // System.Windows e do MSAL com broker, que são Windows-only de verdade. O
        // próprio csproj deste projeto sempre listou "CmlLib" entre o que mora
        // aqui — era a regra que discordava do desenho, não o contrário. O
        // isolamento que interessa é o IGameLauncher: trocar de motor é reescrever
        // uma classe, e nada acima dela sabe que o CmlLib existe. Continua
        // proibido no Core, onde nenhum motor pode entrar.
        ShouldPass(Types.InAssembly(LauncherInfrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.Win32",
                "System.Windows",
                "TCMine.Launcher.Infrastructure.Windows",
                "Microsoft.Identity.Client",
                "TCMine.Server")
            .GetResult());
    }
}
