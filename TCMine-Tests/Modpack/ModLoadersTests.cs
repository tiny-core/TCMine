using TCMine_Domain.Modpack;
using Xunit;

namespace TCMine_Tests.Modpack;

// Parse do id de loader do manifesto CurseForge ("neoforge-21.1.77" → (NeoForge, "21.1.77")).
public class ModLoadersTests
{
    [Theory]
    [InlineData("neoforge-21.1.77", ModLoader.NeoForge, "21.1.77")]
    [InlineData("forge-47.2.0", ModLoader.Forge, "47.2.0")]
    [InlineData("fabric-0.15.11", ModLoader.Fabric, "0.15.11")]
    [InlineData("quilt-0.26.0", ModLoader.Quilt, "0.26.0")]
    public void ParseId_separa_loader_e_versao(string id, ModLoader loader, string version)
    {
        var (l, v) = ModLoaders.ParseId(id);
        Assert.Equal(loader, l);
        Assert.Equal(version, v);
    }

    [Fact]
    public void ParseId_prefixo_desconhecido_cai_no_NeoForge()
    {
        var (l, v) = ModLoaders.ParseId("liteloader-1.2.3");
        Assert.Equal(ModLoader.NeoForge, l);
        Assert.Equal("1.2.3", v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseId_vazio_devolve_NeoForge_sem_versao(string? id)
    {
        var (l, v) = ModLoaders.ParseId(id);
        Assert.Equal(ModLoader.NeoForge, l);
        Assert.Equal(string.Empty, v);
    }

    [Fact]
    public void ParseId_sem_hifen_trata_tudo_como_nome()
    {
        var (l, v) = ModLoaders.ParseId("fabric");
        Assert.Equal(ModLoader.Fabric, l);
        Assert.Equal(string.Empty, v);
    }

    [Fact]
    public void ParseId_e_case_insensitive()
    {
        var (l, _) = ModLoaders.ParseId("NeoForge-21.1.77");
        Assert.Equal(ModLoader.NeoForge, l);
    }
}