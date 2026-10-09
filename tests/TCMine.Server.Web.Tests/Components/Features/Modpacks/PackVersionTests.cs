using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Tests.Components.Features.Modpacks;

/// <summary>
///     A sugestão de número para a próxima versão.
///     O caso que importa é a primeira versão de um modpack: sem versão
///     anterior não há patch nenhum para incrementar, e tratar a ausência como
///     se fosse "1.0.0 já publicado" sugeria 1.0.1-alpha para quem nunca
///     publicou nada.
/// </summary>
public sealed class PackVersionTests
{
    [Fact]
    public void Sem_versao_anterior_sugere_1_0_0_alpha() => PackVersion.SuggestNext(null).ShouldBe("1.0.0-alpha");

    [Fact]
    public void Com_versao_anterior_incrementa_o_patch() => PackVersion.SuggestNext("1.2.3").ShouldBe("1.2.4-alpha");

    [Fact]
    public void Versao_anterior_release_tambem_sugere_alpha()
    {
        // A sugestão é sempre um rascunho novo, independente do canal de onde
        // partiu — publicar é decisão separada, feita depois.
        PackVersion.SuggestNext("2.0.0").ShouldBe("2.0.1-alpha");
    }
}
