using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     A RAM de uma instância.
///     O campo já existia no manifesto e já sobrevivia às atualizações; o que
///     faltava era alguém conseguir mudá-lo. O risco desta operação não é o
///     número — é reescrever o manifesto, que é o mesmo ficheiro de que o diff
///     da próxima atualização depende.
/// </summary>
public class SetInstanceMemoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grava_o_valor_escolhido()
    {
        var store = new FakeInstanceStore();
        var instance = Instalada();

        var result = await new SetInstanceMemory(store).HandleAsync(instance, 8192, Ct);

        result.Succeeded.ShouldBeTrue();
        store.Manifests[instance.Key].MemoryMb.ShouldBe(8192);
    }

    [Fact]
    public async Task Vazio_volta_a_recomendada_do_pack()
    {
        // Apagar o número é uma escolha — "decide tu" —, não um engano a
        // corrigir com o valor anterior.
        var store = new FakeInstanceStore();
        var instance = Instalada(4096);

        await new SetInstanceMemory(store).HandleAsync(instance, null, Ct);

        store.Manifests[instance.Key].MemoryMb.ShouldBeNull();
    }

    [Fact]
    public async Task Abaixo_do_minimo_e_recusado_sem_gravar()
    {
        // Abaixo de 512 MB o jogo nem arranca. Gravar e deixar falhar depois
        // esconderia a causa atrás de um crash da JVM.
        var store = new FakeInstanceStore();
        var instance = Instalada(4096);

        var result = await new SetInstanceMemory(store).HandleAsync(instance, 256, Ct);

        result.Succeeded.ShouldBeFalse();
        store.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task O_resto_do_manifesto_sobrevive()
    {
        // ESTE é o risco real: o manifesto é o conjunto gerenciado que o próximo
        // diff vai ler, e regravá-lo pela metade faria a atualização seguinte
        // achar que os ficheiros em falta na lista são lixo — e apagá-los.
        var store = new FakeInstanceStore();
        var instance = Instalada();

        await new SetInstanceMemory(store).HandleAsync(instance, 2048, Ct);

        var stored = store.Manifests[instance.Key];

        stored.ManagedFiles.Keys.ShouldBe(["mods/jei.jar"]);
        stored.MinecraftVersion.ShouldBe("1.21.1");
        stored.Loader.ShouldBe(ModLoader.NeoForge);
        stored.ModpackVersionId.ShouldBe(instance.Manifest.ModpackVersionId);
    }

    private static InstalledInstance Instalada(int? memoria = null) =>
        new(InstanceKey.New(),
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = Guid.CreateVersion7(),
                ModpackVersionId = Guid.CreateVersion7(),
                ModpackName = "Pack",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string> { ["mods/jei.jar"] = "aa" },
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge,
                MemoryMb = memoria
            },
            0,
            "/instancias/pack");
}
