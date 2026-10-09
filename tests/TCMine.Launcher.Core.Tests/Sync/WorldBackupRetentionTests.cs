using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Sync;

/// <summary>
///     O que se perde quando as cópias de mundo acumulam.
///     Uma política de retenção decide o que destruir, então é a única parte
///     disto que merece teste: apagar o ficheiro é trivial, escolher o errado
///     não tem volta.
/// </summary>
public class WorldBackupRetentionTests
{
    [Fact]
    public void Guarda_as_mais_recentes_e_deixa_cair_as_velhas()
    {
        var names = Enumerable.Range(1, 8).Select(i => $"saves-2026010{i}-120000.zip").ToArray();

        var expiradas = WorldBackupRetention.Expired(names, 5);

        expiradas.ShouldBe(
            ["saves-20260103-120000.zip", "saves-20260102-120000.zip", "saves-20260101-120000.zip"],
            true);
    }

    [Fact]
    public void A_mais_recente_nunca_expira()
    {
        // Esta função corre logo a seguir a criar uma cópia. Apagá-la seria
        // destruir exatamente a que a atualização acabou de exigir.
        var names = new[] { "saves-20260101-120000.zip", "saves-20260102-120000.zip", "saves-20260103-120000.zip" };

        WorldBackupRetention.Expired(names, 1)
            .ShouldNotContain("saves-20260103-120000.zip");
    }

    [Fact]
    public void Abaixo_do_limite_nada_expira()
    {
        var names = new[] { "saves-20260101-120000.zip", "saves-20260102-120000.zip" };

        WorldBackupRetention.Expired(names, 5).ShouldBeEmpty();
    }

    [Fact]
    public void Retencao_zero_e_ilimitado_e_nao_apagar_tudo()
    {
        // A leitura oposta seria catastrófica e silenciosa: "guardar zero" a
        // apagar todas as cópias do jogador na primeira atualização. É a mesma
        // convenção do painel.
        var names = new[] { "saves-20260101-120000.zip", "saves-20260102-120000.zip" };

        WorldBackupRetention.Expired(names, 0).ShouldBeEmpty();
    }

    [Fact]
    public void A_ordem_vem_do_NOME_e_nao_da_ordem_de_chegada()
    {
        // O nome carrega a data em formato ordenável, de propósito: depender da
        // data do sistema de ficheiros quebraria com uma cópia movida de sítio.
        var foraDeOrdem = new[]
        {
            "saves-20260102-120000.zip", "saves-20260105-120000.zip", "saves-20260101-120000.zip"
        };

        WorldBackupRetention.Expired(foraDeOrdem, 2).ShouldBe(["saves-20260101-120000.zip"]);
    }
}
