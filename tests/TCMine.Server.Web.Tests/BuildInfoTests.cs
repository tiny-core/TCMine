using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     A versão mostrada no menu.
///     Existe porque o rótulo só serve se for verdade: uma build sem versão
///     declarada tem 1.0.0 por padrão do SDK, e exibir isso seria pior que não
///     exibir nada — o operador leria "1.0.0" numa instalação 0.1.x e não teria
///     como desconfiar.
/// </summary>
public sealed class BuildInfoTests
{
    [Fact]
    public void Nunca_devolve_vazio()
    {
        BuildInfo.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Build_local_se_identifica_como_dev()
    {
        // A suíte roda sem -p:Version, então cai no padrão do SDK. Se este teste
        // falhar mostrando "1.0.0", a regra de tratar o padrão como "dev" se
        // perdeu — e a versão passaria a mentir em toda instalação.
        BuildInfo.Version.ShouldBe("dev");
    }

    [Fact]
    public void Nao_expoe_o_hash_do_commit()
    {
        // O SDK acrescenta "+<sha>" ao InformationalVersion quando o repositório
        // é conhecido. Não cabe num rodapé e não diz nada a quem opera.
        BuildInfo.Version.ShouldNotContain("+");
    }
}
