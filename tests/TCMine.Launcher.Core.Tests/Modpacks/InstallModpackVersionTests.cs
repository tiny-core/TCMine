using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     A instalação, que é o mesmo que a atualização.
///     O modelo é declarativo: o manifesto descreve o estado final e o diff diz a
///     diferença. Por isso não há caminho separado para "primeira vez" — um diff
///     contra uma instância vazia já É a instalação completa.
///     O teste que mais importa nesta classe é o do manifesto local. Ele é a
///     fronteira entre o que é nosso e o que é do jogador, e passar a coisa
///     errada ao differ apaga mundos.
/// </summary>
public class InstallModpackVersionTests
{
    private static readonly Uri Servidor = new("https://servidor.exemplo/");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Instalacao_limpa_baixa_e_materializa_tudo()
    {
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("config/jei.toml", "bb"));

        var cenario = new Cenario(pack, versao);

        var resultado = await cenario.Instalar();

        resultado.Succeeded.ShouldBeTrue(resultado.Error);
        cenario.Downloader.Requested.ShouldBe(["aa", "bb"], ignoreOrder: true);
        cenario.Content.Materialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Arquivo_ja_no_store_nao_e_baixado_de_novo()
    {
        // O ganho do store compartilhado: um mod que outro modpack já trouxe
        // custa zero de rede.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("mods/rei.jar", "bb"));

        var cenario = new Cenario(pack, versao);
        cenario.Content.Hashes.Add("aa");

        await cenario.Instalar();

        cenario.Downloader.Requested.ShouldBe(["bb"]);

        // Mas ele É materializado: estar no store não o coloca na instância.
        cenario.Content.Materialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Apenas_mods_recebem_hardlink()
    {
        // Ligar um config corromperia o blob COMPARTILHADO na primeira vez que o
        // jogo o reescrevesse, e a corrupção viajaria para todas as instâncias.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("config/jei.toml", "bb"));

        var cenario = new Cenario(pack, versao);

        await cenario.Instalar();

        cenario.Content.Materialized.Single(m => m.Key.Contains("jei.jar")).Value.ShouldBeTrue();
        cenario.Content.Materialized.Single(m => m.Key.Contains("jei.toml")).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task O_diff_usa_o_manifesto_local_e_nunca_o_disco()
    {
        // ESTE é o guard. O conjunto gerenciado vem do manifesto que gravamos; um
        // arquivo do jogador (um mundo, um screenshot, o options.txt) jamais entra
        // no cálculo, porque ele nunca esteve no manifesto. Se algum dia alguém
        // trocar isto por uma varredura da pasta, o primeiro update apaga tudo.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var cenario = new Cenario(pack, versao);

        cenario.Instances.Manifests[new InstanceKey(pack.Id, versao.Id)] = new InstanceManifest
        {
            Schema = 1,
            ModpackId = pack.Id,
            ModpackVersionId = versao.Id,
            ModpackName = pack.Name,
            Version = "1.0.0",
            InstalledAt = DateTimeOffset.UtcNow,

            // O manifesto anterior conhece um mod que saiu do pack. Só ele pode
            // ser apagado.
            ManagedFiles = new Dictionary<string, string> { ["mods/jei.jar"] = "aa", ["mods/velho.jar"] = "cc" }
        };

        await cenario.Instalar();

        cenario.Instances.Deleted.ShouldBe(["mods/velho.jar"]);
    }

    [Fact]
    public async Task Arquivo_intocado_nao_e_rebaixado_nem_rematerializado()
    {
        // O hash bate: não há trabalho a fazer. Sem isto, cada abertura do
        // launcher reinstalaria o pack inteiro.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var cenario = new Cenario(pack, versao);
        cenario.Content.Hashes.Add("aa");

        cenario.Instances.Manifests[new InstanceKey(pack.Id, versao.Id)] = Manifesto(
            pack, versao, new Dictionary<string, string> { ["mods/jei.jar"] = "aa" });

        await cenario.Instalar();

        cenario.Downloader.Requested.ShouldBeEmpty();
        cenario.Content.Materialized.ShouldBeEmpty();
        cenario.Instances.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Arquivos_do_servidor_ficam_de_fora()
    {
        // O mesmo manifesto serve os dois lados. Baixar um mod server-only não
        // daria erro visível, mas custaria banda e disco à toa.
        var pack = Modpack();

        var versao = Versao(
            pack.Id,
            Arquivo("mods/jei.jar", "aa"),
            Arquivo("mods/spark-server.jar", "bb", FileSide.ServerOnly));

        var cenario = new Cenario(pack, versao);

        await cenario.Instalar();

        cenario.Downloader.Requested.ShouldBe(["aa"]);
    }

    [Fact]
    public async Task O_manifesto_gravado_descreve_o_estado_final_e_nao_o_trabalho_feito()
    {
        // Registrar só o que esta execução mexeu faria o diff seguinte achar que
        // os arquivos intocados são lixo — e apagá-los.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("mods/rei.jar", "bb"));

        var cenario = new Cenario(pack, versao);
        cenario.Content.Hashes.Add("aa");
        cenario.Instances.Manifests[new InstanceKey(pack.Id, versao.Id)] = Manifesto(
            pack, versao, new Dictionary<string, string> { ["mods/jei.jar"] = "aa" });

        await cenario.Instalar();

        var gravado = cenario.Instances.Manifests[new InstanceKey(pack.Id, versao.Id)];

        gravado.ManagedFiles.Keys.ShouldBe(["mods/jei.jar", "mods/rei.jar"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_ram_escolhida_pelo_jogador_sobrevive_a_atualizacao()
    {
        // Ela é dele, não do pack. Voltar para a recomendada a cada versão nova
        // desfaria em silêncio um ajuste que ele fez de propósito.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));
        var chave = new InstanceKey(pack.Id, versao.Id);

        var cenario = new Cenario(pack, versao);
        cenario.Instances.Manifests[chave] = Manifesto(pack, versao, []) with { MemoryMb = 8192 };

        await cenario.Instalar();

        cenario.Instances.Manifests[chave].MemoryMb.ShouldBe(8192);
    }

    [Fact]
    public async Task Modpack_sem_versao_publicada_explica_em_vez_de_falhar()
    {
        var pack = Modpack();
        var cenario = new Cenario(pack, versao: null);

        var resultado = await cenario.InstalarUltima();

        resultado.Succeeded.ShouldBeFalse();
        resultado.Error!.ShouldContain("ainda não tem uma versão publicada");
    }

    [Fact]
    public async Task Falha_no_meio_vira_mensagem_e_o_manifesto_nao_e_gravado()
    {
        // Gravar um manifesto de uma instalação que não terminou faria o próximo
        // diff acreditar que os arquivos estão lá, e nada seria baixado de novo.
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var cenario = new Cenario(pack, versao);
        cenario.Connection.Throws = new InvalidOperationException("canal caiu");

        var resultado = await cenario.Instalar();

        resultado.Succeeded.ShouldBeFalse();
        cenario.Instances.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task O_progresso_termina_em_Done()
    {
        var pack = Modpack();
        var versao = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var cenario = new Cenario(pack, versao);
        var fases = new List<InstallPhase>();

        await cenario.Instalar(new Progress<InstallProgress>(p => fases.Add(p.Phase)));

        // Progress<T> posta no contexto de sincronização, então a ordem exata não
        // é garantida num teste; o que importa é que a conclusão foi anunciada.
        fases.ShouldContain(InstallPhase.Done);
    }

    // ---------- apoio ----------

    private sealed class Cenario
    {
        public Cenario(ModpackDto pack, ModpackVersionDto? versao)
        {
            Pack = pack;

            if (versao is not null)
            {
                Connection.Versions[versao.Id] = versao;
                Connection.Latest[pack.Id] = versao;
            }

            Versao = versao;
        }

        public ModpackDto Pack { get; }

        public ModpackVersionDto? Versao { get; }

        public FakeServerConnection Connection { get; } = new();

        public FakeContentStore Content { get; } = new();

        public FakeBlobDownloader Downloader { get; } = new();

        public FakeInstanceStore Instances { get; } = new();

        private InstallModpackVersion Instalador => new(Connection, Content, Downloader, Instances);

        public Task<InstallResult> Instalar(IProgress<InstallProgress>? progresso = null) =>
            Instalador.HandleAsync(Servidor, Pack, Versao!.Id, progresso, Ct);

        public Task<InstallResult> InstalarUltima() =>
            Instalador.InstallLatestAsync(Servidor, Pack, null, Ct);
    }

    private static ModpackDto Modpack() => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = "pack",
        Name = "Pack",
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge
    };

    private static ModpackVersionDto Versao(Guid modpackId, params ModpackFileDto[] arquivos) => new()
    {
        Id = Guid.CreateVersion7(),
        ModpackId = modpackId,
        Version = "1.2.0",
        LoaderVersion = "21.1.100",
        State = ModpackVersionState.Ready,
        PublishedAt = DateTimeOffset.UtcNow,
        RecommendedMemoryMb = 4096,
        Files = arquivos
    };

    private static ModpackFileDto Arquivo(string caminho, string sha, FileSide side = FileSide.Both) => new()
    {
        Path = caminho, Sha256 = sha, SizeBytes = 10, Side = side
    };

    private static InstanceManifest Manifesto(
        ModpackDto pack, ModpackVersionDto versao, Dictionary<string, string> arquivos) => new()
    {
        Schema = 1,
        ModpackId = pack.Id,
        ModpackVersionId = versao.Id,
        ModpackName = pack.Name,
        Version = versao.Version,
        InstalledAt = DateTimeOffset.UtcNow,
        ManagedFiles = arquivos
    };
}
