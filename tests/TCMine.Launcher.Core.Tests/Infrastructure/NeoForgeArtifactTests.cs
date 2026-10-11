using TCMine.Launcher.Infrastructure.Game;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

public sealed class NeoForgeArtifactTests : IDisposable
{
    private readonly string _versions = Path.Combine(Path.GetTempPath(), $"tcmine-nf-{Guid.CreateVersion7():N}");

    public void Dispose()
    {
        if (Directory.Exists(_versions))
            Directory.Delete(_versions, true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NeoForge_atual_usa_o_artefato_proprio()
    {
        var artifact = NeoForgeArtifact.For("1.21.1", "21.1.200");

        artifact.InstallerUrl.AbsoluteUri.ShouldBe(
            "https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.200/neoforge-21.1.200-installer.jar");
        artifact.InstallFlag.ShouldBe("--install-client");
        artifact.VersionName.ShouldBe("neoforge-21.1.200");
        artifact.IsLegacy.ShouldBeFalse();
    }

    [Fact]
    public void NeoForge_do_1_20_1_usa_o_artefato_herdado_do_Forge()
    {
        // Regressão: o painel passou a oferecer o NeoForge do 1.20.1 e o
        // launcher pedia neoforge/47.1.106, que não existe no maven.
        var artifact = NeoForgeArtifact.For("1.20.1", "47.1.106");

        artifact.InstallerUrl.AbsoluteUri.ShouldBe(
            "https://maven.neoforged.net/releases/net/neoforged/forge/1.20.1-47.1.106/forge-1.20.1-47.1.106-installer.jar");
        artifact.InstallFlag.ShouldBe("--installClient");
        artifact.VersionName.ShouldBe("1.20.1-forge-47.1.106");
        artifact.IsLegacy.ShouldBeTrue();
    }

    [Fact]
    public void Build_do_1_20_1_ja_com_o_prefixo_do_maven_nao_e_duplicada()
    {
        NeoForgeArtifact.For("1.20.1", "1.20.1-47.1.106")
            .ShouldBe(NeoForgeArtifact.For("1.20.1", "47.1.106"));
    }

    [Fact]
    public void Sem_nada_no_disco_nao_ha_versao_instalada()
    {
        // A pasta nem existe: é o primeiro arranque de qualquer jogador.
        NeoForgeArtifact.For("1.20.1", "47.1.106").FindInstalled(_versions).ShouldBeNull();
        NeoForgeArtifact.For("1.21.1", "21.1.200").FindInstalled(_versions).ShouldBeNull();
    }

    [Fact]
    public void Versao_com_o_nome_esperado_e_reconhecida()
    {
        Install("neoforge-21.1.200");

        NeoForgeArtifact.For("1.21.1", "21.1.200").FindInstalled(_versions).ShouldBe("neoforge-21.1.200");
    }

    [Fact]
    public void No_1_20_1_vale_o_nome_que_o_instalador_escolheu()
    {
        // Quem batiza a versão é o instalador. Se ele não usar o nome que
        // esperamos, a instalação não pode ser dada como falha.
        Install("1.20.1-neoforge-47.1.106");

        NeoForgeArtifact.For("1.20.1", "47.1.106").FindInstalled(_versions).ShouldBe("1.20.1-neoforge-47.1.106");
    }

    [Fact]
    public void Outra_build_do_1_20_1_nao_e_confundida_com_a_pedida()
    {
        Install("1.20.1-forge-47.1.79");

        NeoForgeArtifact.For("1.20.1", "47.1.106").FindInstalled(_versions).ShouldBeNull();
    }

    [Fact]
    public void Pasta_sem_o_json_da_versao_nao_conta_como_instalada()
    {
        // Instalação interrompida deixa a pasta e não o JSON; tratá-la como
        // pronta faria o jogo falhar ao abrir, para sempre.
        Directory.CreateDirectory(Path.Combine(_versions, "1.20.1-forge-47.1.106"));

        NeoForgeArtifact.For("1.20.1", "47.1.106").FindInstalled(_versions).ShouldBeNull();
    }

    private void Install(string name)
    {
        var directory = Path.Combine(_versions, name);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.json"), "{}");
    }
}
