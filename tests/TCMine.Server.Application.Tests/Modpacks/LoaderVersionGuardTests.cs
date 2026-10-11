using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Tests.Fakes;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Uma build de loader que não existe só falhava quando o servidor de jogo
///     tentava subir, num log de container. Estas são as regras que trazem o
///     erro para quem digitou o número — sem recusar o que apenas não se
///     conseguiu conferir.
/// </summary>
public sealed class LoaderVersionGuardTests
{
    [Fact]
    public async Task Build_que_esta_no_catalogo_passa()
    {
        var result = await new FakeVersionCatalog("21.1.259", "21.1.258").Guard()
            .CheckAsync(ModLoader.NeoForge, "1.21.1", " 21.1.258 ", CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Build_fora_do_catalogo_e_recusada_e_o_erro_sugere_a_mais_recente()
    {
        var result = await new FakeVersionCatalog("21.1.259", "21.1.258").Guard()
            .CheckAsync(ModLoader.NeoForge, "1.21.1", "21.1.999", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("21.1.259", result.Error);
    }

    [Fact]
    public async Task Loader_sem_nenhuma_build_para_aquele_minecraft_e_recusado()
    {
        // A fonte respondeu, e respondeu "nenhuma": é um fato, não uma dúvida.
        var result = await new FakeVersionCatalog().Guard()
            .CheckAsync(ModLoader.Fabric, "1.2.5", "0.16.5", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Catalogo_fora_do_ar_deixa_passar()
    {
        // Não saber não é motivo para recusar: uma fonte externa instável não
        // pode impedir ninguém de publicar.
        var result = await new FakeVersionCatalog { Failed = true }.Guard()
            .CheckAsync(ModLoader.NeoForge, "1.21.1", "21.1.999", CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(ModLoader.Vanilla, "qualquer")] // vanilla não tem loader
    [InlineData(ModLoader.NeoForge, "  ")] // em branco = a recomendada
    [InlineData(ModLoader.NeoForge, null)]
    public async Task Sem_loader_ou_sem_build_nao_ha_o_que_conferir(ModLoader loader, string? loaderVersion)
    {
        var result = await new FakeVersionCatalog().Guard()
            .CheckAsync(loader, "1.21.1", loaderVersion, CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Forge_antigo_aceita_a_build_sem_o_sufixo_do_minecraft()
    {
        // No maven as builds antigas do Forge repetem o Minecraft no fim, e os
        // manifestos de pack trazem só a primeira parte.
        var guard = new FakeVersionCatalog("10.13.4.1614-1.7.10").Guard();

        Assert.True((await guard.CheckAsync(ModLoader.Forge, "1.7.10", "10.13.4.1614", CancellationToken.None))
            .Succeeded);

        // A tolerância é só do Forge, e só para o sufixo inteiro.
        Assert.False((await guard.CheckAsync(ModLoader.Forge, "1.7.10", "10.13.4", CancellationToken.None))
            .Succeeded);
    }
}
