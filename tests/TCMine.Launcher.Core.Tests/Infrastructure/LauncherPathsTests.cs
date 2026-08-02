using TCMine.Launcher.Infrastructure;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

public class LauncherPathsTests
{
    private static readonly LauncherPaths Paths = new(@"C:\TCMine");

    [Fact]
    public void Servidores_diferentes_recebem_pastas_diferentes()
    {
        // É o que permite jogar em dois servidores na mesma máquina sem que
        // um sobrescreva a instância do outro.
        var a = Paths.InstanceRootFor(new Uri("https://mc.exemplo.com"));
        var b = Paths.InstanceRootFor(new Uri("https://outro.exemplo.com"));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Mesma_url_gera_sempre_a_mesma_pasta()
    {
        // Se o hash variasse entre execuções, cada inicialização criaria uma
        // instância nova e o disco encheria de cópias do mesmo modpack.
        var primeira = Paths.InstanceRootFor(new Uri("https://mc.exemplo.com"));
        var segunda = Paths.InstanceRootFor(new Uri("https://mc.exemplo.com"));

        segunda.ShouldBe(primeira);
    }

    [Fact]
    public void O_store_fica_fora_das_instancias()
    {
        // O content store é compartilhado de propósito: um mod usado por
        // dois servidores ocupa um arquivo só.
        Paths.StoreDirectory.ShouldNotContain("instances");
    }
}
