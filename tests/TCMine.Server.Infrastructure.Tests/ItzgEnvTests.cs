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
