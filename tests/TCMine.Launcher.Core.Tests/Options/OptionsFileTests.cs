using TCMine.Launcher.Core.Options;

namespace TCMine.Launcher.Core.Tests.Options;

/// <summary>
///     Ler e fundir o options.txt. Errar aqui é apagar a configuração de alguém:
///     uma tecla perdida volta ao padrão sem aviso nenhum.
/// </summary>
public class OptionsFileTests
{
    private const string Local = """
                                 version:3955
                                 key_key.jump:key.keyboard.space
                                 key_key.jei.showRecipe:key.keyboard.r
                                 fov:0.5
                                 renderDistance:12
                                 """;

    [Fact]
    public void So_as_teclas_e_a_versao_vao_para_a_conta()
    {
        var payload = OptionsFile.Extract(Local, includeRest: false);

        payload.KeyBindings.ShouldBe(
            "version:3955\nkey_key.jump:key.keyboard.space\nkey_key.jei.showRecipe:key.keyboard.r");
        payload.Rest.ShouldBeNull();
    }

    [Fact]
    public void Com_tudo_ligado_o_resto_vai_junto()
    {
        OptionsFile.Extract(Local, includeRest: true).Rest.ShouldBe("fov:0.5\nrenderDistance:12");
    }

    [Fact]
    public void A_tecla_da_conta_troca_no_lugar_e_o_resto_fica()
    {
        var merged = OptionsFile.Merge(Local, "version:3955\nkey_key.jump:key.keyboard.j", null);

        merged.ShouldBe("""
                        version:3955
                        key_key.jump:key.keyboard.j
                        key_key.jei.showRecipe:key.keyboard.r
                        fov:0.5
                        renderDistance:12

                        """.Replace("\r", ""));
    }

    [Fact]
    public void Tecla_que_so_existe_aqui_nao_e_apagada()
    {
        // A de um mod que o outro PC ainda não conheceu: apagá-la voltava-a ao padrão.
        var merged = OptionsFile.Merge(Local, "key_key.sneak:key.keyboard.c", null);

        merged.ShouldContain("key_key.jei.showRecipe:key.keyboard.r");
        merged.ShouldContain("key_key.sneak:key.keyboard.c");
    }

    [Fact]
    public void A_versao_deste_PC_vence()
    {
        // Quem a escreveu foi o jogo daqui; trocar faria o jogo converter o arquivo.
        OptionsFile.Merge(Local, "version:9999", null).ShouldContain("version:3955");
    }

    [Fact]
    public void Arquivo_novo_recebe_a_versao_no_topo()
    {
        var merged = OptionsFile.Merge(null, "key_key.jump:key.keyboard.j\nversion:3955", null);

        merged.ShouldBe("version:3955\nkey_key.jump:key.keyboard.j\n");
    }

    [Fact]
    public void Sem_o_resto_os_graficos_daqui_ficam()
    {
        OptionsFile.Merge(Local, "key_key.jump:key.keyboard.j", null).ShouldContain("renderDistance:12");
    }

    [Fact]
    public void Com_o_resto_os_graficos_da_conta_entram()
    {
        OptionsFile.Merge(Local, "key_key.jump:key.keyboard.j", "renderDistance:6")
            .ShouldContain("renderDistance:6");
    }

    [Fact]
    public void Arquivo_com_fim_de_linha_do_Windows_le_igual()
    {
        var payload = OptionsFile.Extract(Local.Replace("\n", "\r\n"), includeRest: false);

        payload.Fingerprint.ShouldBe(OptionsFile.Extract(Local, includeRest: false).Fingerprint);
    }
}
