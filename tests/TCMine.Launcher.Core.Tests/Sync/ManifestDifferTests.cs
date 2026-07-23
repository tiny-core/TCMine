using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Sync;

public class ManifestDifferTests
{
    private static readonly InstanceKey Instancia =
        new(Guid.CreateVersion7(), Guid.CreateVersion7());

    // ---------- Helpers ----------
    // Montar um ModpackVersionDto inteiro em cada teste esconderia o que
    // realmente importa. Estes dois métodos deixam cada caso com duas ou três
    // linhas de setup, e o que varia fica evidente.

    private static ModpackVersionDto Manifest(params ModpackFileDto[] arquivos)
    {
        return new ModpackVersionDto
        {
            Id = Instancia.ModpackVersionId,
            ModpackId = Instancia.ModpackId,
            Version = "1.0.0",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge,
            LoaderVersion = "21.1.0",
            State = ModpackVersionState.Ready,
            PublishedAt = DateTimeOffset.UtcNow,
            Files = arquivos
        };
    }

    private static ModpackFileDto Arquivo(
        string caminho,
        string hash,
        FileSide lado = FileSide.Both,
        bool opcional = false)
    {
        return new ModpackFileDto
        {
            Path = caminho,
            Sha256 = hash,
            SizeBytes = 1024,
            Side = lado,
            Optional = opcional
        };
    }

    private static SyncPlan Planejar(
        ModpackVersionDto manifest,
        Dictionary<string, string>? disco = null,
        HashSet<string>? store = null,
        bool comOpcionais = false)
    {
        return ManifestDiffer.Plan(
            Instancia,
            manifest,
            disco ?? [],
            store ?? [],
            comOpcionais);
    }

    // ---------- Casos ----------

    [Fact]
    public void Instalacao_do_zero_baixa_e_materializa_tudo()
    {
        var plano = Planejar(Manifest(
            Arquivo("mods/jei.jar", "aaa"),
            Arquivo("mods/create.jar", "bbb")));

        plano.ToDownload.Count.ShouldBe(2);
        plano.ToMaterialize.Count.ShouldBe(2);
        plano.IsUpToDate.ShouldBeFalse();
    }

    [Fact]
    public void Arquivo_ja_correto_no_disco_nao_entra_no_plano()
    {
        var plano = Planejar(
            Manifest(Arquivo("mods/jei.jar", "aaa")),
            new Dictionary<string, string> { ["mods/jei.jar"] = "aaa" });

        plano.IsUpToDate.ShouldBeTrue();
    }

    [Fact]
    public void Arquivo_ja_no_store_e_materializado_sem_baixar()
    {
        // Este é o ganho de compartilhar o content store entre modpacks:
        // instalar um pack novo que reusa mods de outro é quase instantâneo.
        var plano = Planejar(
            Manifest(Arquivo("mods/jei.jar", "aaa")),
            store: ["aaa"]);

        plano.ToDownload.ShouldBeEmpty();
        plano.ToMaterialize.Count.ShouldBe(1);
    }

    [Fact]
    public void Arquivo_com_hash_diferente_e_substituido()
    {
        // Acontece quando o pack atualiza a versão de um mod: mesmo caminho,
        // conteúdo novo.
        var plano = Planejar(
            Manifest(Arquivo("mods/jei.jar", "novo")),
            new Dictionary<string, string> { ["mods/jei.jar"] = "antigo" });

        plano.ToDownload.Count.ShouldBe(1);
        plano.ToMaterialize.Count.ShouldBe(1);
    }

    [Fact]
    public void Mod_removido_do_pack_e_apagado()
    {
        // Sem esta limpeza o mod continuaria carregando e travaria provavelmente
        //  o jogo por incompatibilidade com a versão nova.
        var plano = Planejar(
            Manifest(Arquivo("mods/jei.jar", "aaa")),
            new Dictionary<string, string>
            {
                ["mods/jei.jar"] = "aaa",
                ["mods/removido.jar"] = "ccc"
            },
            ["aaa"]);

        plano.ToDelete.ShouldBe(["mods/removido.jar"]);
    }

    [Fact]
    public void Arquivo_server_only_e_ignorado_no_cliente()
    {
        // O mesmo mrpack serve os dois lados. Baixar um mod de servidor não
        // daria erro visível, mas gastaria banda e disco à toa.
        var plano = Planejar(Manifest(
            Arquivo("mods/spark.jar", "bbb", FileSide.ServerOnly)));

        plano.IsUpToDate.ShouldBeTrue();
    }

    [Fact]
    public void Arquivo_client_only_e_incluido()
    {
        var plano = Planejar(Manifest(
            Arquivo("mods/optifine.jar", "ddd", FileSide.ClientOnly)));

        plano.ToDownload.Count.ShouldBe(1);
    }

    [Fact]
    public void Opcional_fica_de_fora_quando_o_jogador_nao_pediu()
    {
        var plano = Planejar(
            Manifest(Arquivo("shaderpacks/bsl.zip", "eee", opcional: true)),
            comOpcionais: false);

        plano.IsUpToDate.ShouldBeTrue();
    }

    [Fact]
    public void Opcional_entra_quando_o_jogador_pediu()
    {
        var plano = Planejar(
            Manifest(Arquivo("shaderpacks/bsl.zip", "eee", opcional: true)),
            comOpcionais: true);

        plano.ToDownload.Count.ShouldBe(1);
    }

    [Fact]
    public void Opcional_desmarcado_depois_e_apagado_do_disco()
    {
        // O jogador tinha shaders e desativou. Como o arquivo deixa de ser
        // desejado, ele cai na lista de deleção.
        var plano = Planejar(
            Manifest(Arquivo("shaderpacks/bsl.zip", "eee", opcional: true)),
            new Dictionary<string, string> { ["shaderpacks/bsl.zip"] = "eee" },
            comOpcionais: false);

        plano.ToDelete.ShouldBe(["shaderpacks/bsl.zip"]);
    }

    [Fact]
    public void Comparacao_de_hash_ignora_a_caixa()
    {
        // Fontes diferentes escrevem hex em maiúscula ou minúscula. Sem o
        // OrdinalIgnoreCase, um arquivo já correto seria baixado de novo a
        // cada verificação.
        var plano = Planejar(
            Manifest(Arquivo("mods/jei.jar", "AAA")),
            new Dictionary<string, string> { ["mods/jei.jar"] = "aaa" });

        plano.IsUpToDate.ShouldBeTrue();
    }

    [Fact]
    public void Bytes_a_baixar_somam_apenas_o_que_falta()
    {
        // O que alimenta a barra de progresso. Contar o que já está no store
        // faria a barra pular do nada para o fim.
        var plano = Planejar(
            Manifest(
                Arquivo("mods/a.jar", "aaa"),
                Arquivo("mods/b.jar", "bbb")),
            store: ["aaa"]);

        plano.BytesToDownload.ShouldBe(1024);
    }

    [Fact]
    public void Manifest_vazio_nao_apaga_nada_que_nao_exista()
    {
        var plano = Planejar(Manifest());

        plano.IsUpToDate.ShouldBeTrue();
    }
}