using TCMine.Contracts.Modpacks;
using TCMine.Server.Infrastructure.Instances;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O ambiente do container itzg é o que decide QUE jogo sobe. Um nome de
///     variável errado não dá erro: o itzg ignora e escolhe "o mais recente".
/// </summary>
public sealed class ItzgEnvTests
{
    [Theory]
    [InlineData(ModLoader.NeoForge, "NEOFORGE", "NEOFORGE_VERSION=21.1.77")]
    [InlineData(ModLoader.Forge, "FORGE", "FORGE_VERSION=21.1.77")]
    [InlineData(ModLoader.Fabric, "FABRIC", "FABRIC_LOADER_VERSION=21.1.77")]
    [InlineData(ModLoader.Quilt, "QUILT", "QUILT_LOADER_VERSION=21.1.77")]
    public void Versao_do_minecraft_e_build_do_loader_vem_do_modpack(
        ModLoader loader, string type, string loaderVariable)
    {
        var env = ItzgEnv.GameVariables("1.21.1", loader, "21.1.77");

        env.ShouldBe([$"TYPE={type}", "VERSION=1.21.1", loaderVariable]);
    }

    [Fact]
    public void Vanilla_nao_ganha_variavel_de_loader()
    {
        ItzgEnv.GameVariables("1.20.1", ModLoader.Vanilla, "qualquer")
            .ShouldBe(["TYPE=VANILLA", "VERSION=1.20.1"]);
    }

    [Fact]
    public void Build_do_loader_em_branco_e_omitida_e_nao_mandada_vazia()
    {
        ItzgEnv.GameVariables("1.21.1", ModLoader.NeoForge, " ")
            .ShouldBe(["TYPE=NEOFORGE", "VERSION=1.21.1"]);
    }

    [Fact]
    public void Minecraft_em_branco_e_recusado_em_vez_de_virar_latest() =>
        Should.Throw<InvalidOperationException>(() => ItzgEnv.GameVariables("", ModLoader.Forge, "47.2.0"));

    [Theory]
    [InlineData("1.7.10", "java8")]
    [InlineData("1.12.2", "java8")]
    [InlineData("1.16.5", "java8")] // a última do Java 8
    [InlineData("1.17", "java16")]
    [InlineData("1.17.1", "java16")]
    [InlineData("1.18", "java17")]
    [InlineData("1.19.2", "java17")]
    [InlineData("1.20.1", "java17")]
    [InlineData("1.20.4", "java17")] // a última do Java 17
    [InlineData("1.20.5", "java21")] // a primeira do Java 21
    [InlineData("1.20.6", "java21")]
    [InlineData("1.21", "java21")]
    [InlineData("1.21.1", "java21")]
    [InlineData("26.1", "java25")]
    [InlineData("26.1.2", "java25")]
    public void Cada_versao_do_minecraft_sobe_no_java_que_ela_pede(string minecraft, string tag)
    {
        // Era sempre :latest — o Java do Minecraft mais novo. Um pack antigo
        // morria no arranque com um erro que não fala em Java.
        ItzgEnv.JavaTag(minecraft).ShouldBe(tag);
        ItzgEnv.Image(minecraft).ShouldBe($"itzg/minecraft-server:{tag}");
    }

    [Theory]
    [InlineData("1.20.5-rc1", "java21")] // pré-lançamento pede o Java da final
    [InlineData(" 1.21.1 ", "java21")]
    public void Sufixo_e_espacos_nao_mudam_o_java(string minecraft, string tag)
    {
        ItzgEnv.JavaTag(minecraft).ShouldBe(tag);
    }

    [Theory]
    [InlineData("27.1")] // série que a tabela ainda não conhece
    [InlineData("24w14a")] // snapshot
    [InlineData("")]
    public void Versao_que_a_tabela_nao_conhece_vai_para_latest(string minecraft)
    {
        // latest é a tag que o itzg mantém no Java do Minecraft mais recente: é
        // o palpite certo para o que é mais novo do que esta tabela.
        ItzgEnv.JavaTag(minecraft).ShouldBe("latest");
    }

    [Fact]
    public void Opcoes_de_jogo_viram_variaveis_em_ordem_estavel()
    {
        // A ordem entra na impressão digital do container: a mesma configuração
        // tem de dar sempre a mesma lista, ou ele seria recriado à toa.
        ItzgEnv.OptionVariables("""{"VIEW_DISTANCE":"8","DIFFICULTY":"hard","PVP":"false"}""")
            .ShouldBe(["DIFFICULTY=hard", "PVP=false", "VIEW_DISTANCE=8"]);
    }

    [Fact]
    public void Sem_opcoes_nao_ha_variaveis()
    {
        ItzgEnv.OptionVariables(null).ShouldBeEmpty();
    }

    [Fact]
    public void Opcao_fora_do_catalogo_nunca_chega_ao_container()
    {
        // Mesmo gravada à mão no banco: VERSION é do modpack.
        ItzgEnv.OptionVariables("""{"VERSION":"1.8.9","PVP":"true"}""").ShouldBe(["PVP=true"]);
    }

    [Fact]
    public void Impressao_digital_muda_com_a_versao_e_ignora_a_ordem()
    {
        var a = ItzgEnv.Fingerprint("img", ["VERSION=1.21.1", "TYPE=NEOFORGE"], ["port=25565"]);
        var b = ItzgEnv.Fingerprint("img", ["TYPE=NEOFORGE", "VERSION=1.21.1"], ["port=25565"]);
        var c = ItzgEnv.Fingerprint("img", ["TYPE=NEOFORGE", "VERSION=1.20.1"], ["port=25565"]);

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }
}
