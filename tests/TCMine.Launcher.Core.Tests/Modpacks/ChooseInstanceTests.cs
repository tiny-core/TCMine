using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     Qual instância a tela de jogar mostra.
///     A escolha guardada é uma chave, e uma chave sobrevive à pasta: o jogador
///     remove a instância ativa para libertar disco e a escolha continua lá,
///     apontando para o nada. Resolver contra o que está realmente instalado é o
///     que impede a tela de oferecer um pack fantasma — com botão e tudo.
/// </summary>
public class ChooseInstanceTests
{
    [Fact]
    public void A_escolhida_vence_quando_ainda_esta_instalada()
    {
        var a = Instalada("Alpha");
        var b = Instalada("Beta");

        ChooseInstance.Resolve([a, b], b.Key).ShouldBe(b);
    }

    [Fact]
    public void Escolhida_que_foi_removida_nao_aparece_como_ativa()
    {
        // O caso que motivou a função: sem isto, a tela de jogar mostraria o
        // nome de um pack que já não existe no disco.
        var a = Instalada("Alpha");
        var removida = Instalada("Removida").Key;

        ChooseInstance.Resolve([a], removida).ShouldBe(a);
    }

    [Fact]
    public void Com_uma_instancia_so_ela_e_a_ativa_sem_ninguem_escolher()
    {
        // Pedir para "ativar" a única coisa que o jogador tem seria um clique
        // para confirmar o óbvio, logo depois de uma instalação que já demorou.
        var unica = Instalada("Alpha");

        ChooseInstance.Resolve([unica], null).ShouldBe(unica);
    }

    [Fact]
    public void Com_varias_e_nenhuma_escolha_ninguem_e_ativa()
    {
        // Adivinhar (a mais recente, a maior) acertaria às vezes e abriria o
        // pack errado nas outras. A tela pede para escolher.
        ChooseInstance.Resolve([Instalada("Alpha"), Instalada("Beta")], null).ShouldBeNull();
    }

    [Fact]
    public void Sem_nada_instalado_nao_ha_ativa_nem_com_escolha_gravada() =>
        ChooseInstance.Resolve([], Instalada("Alpha").Key).ShouldBeNull();

    [Fact]
    public void A_vista_distingue_os_dois_vazios()
    {
        // "Instale alguma coisa" e "escolha entre as que já tem" são textos e
        // destinos diferentes na tela de jogar.
        new ActiveInstanceView(null, 0).NeedsChoice.ShouldBeFalse();
        new ActiveInstanceView(null, 3).NeedsChoice.ShouldBeTrue();
        new ActiveInstanceView(Instalada("Alpha"), 3).NeedsChoice.ShouldBeFalse();
    }

    private static InstalledInstance Instalada(string name)
    {
        var key = InstanceKey.New();

        return new InstalledInstance(
            key,
            new InstanceManifest
            {
                Schema = 1,
                ModpackId = Guid.CreateVersion7(),
                ModpackVersionId = Guid.CreateVersion7(),
                ModpackName = name,
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>()
            },
            0,
            $"/instancias/{name}");
    }
}
