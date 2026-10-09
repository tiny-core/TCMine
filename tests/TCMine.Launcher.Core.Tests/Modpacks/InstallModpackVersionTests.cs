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
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("config/jei.toml", "bb"));

        var scenario = new Cenario(pack, version);

        var result = await scenario.Instalar();

        result.Succeeded.ShouldBeTrue(result.Error);
        scenario.Downloader.Requested.ShouldBe(["aa", "bb"], true);
        scenario.Content.Materialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Arquivo_ja_no_store_nao_e_baixado_de_novo()
    {
        // O ganho do store compartilhado: um mod que outro modpack já trouxe
        // custa zero de rede.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("mods/rei.jar", "bb"));

        var scenario = new Cenario(pack, version);
        scenario.Content.Hashes.Add("aa");

        await scenario.Instalar();

        scenario.Downloader.Requested.ShouldBe(["bb"]);

        // Mas ele É materializado: estar no store não o coloca na instância.
        scenario.Content.Materialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Apenas_mods_recebem_hardlink()
    {
        // Ligar um config corromperia o blob COMPARTILHADO na primeira vez que o
        // jogo o reescrevesse, e a corrupção viajaria para todas as instâncias.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("config/jei.toml", "bb"));

        var scenario = new Cenario(pack, version);

        await scenario.Instalar();

        scenario.Content.Materialized.Single(m => m.Key.Contains("jei.jar")).Value.ShouldBeTrue();
        scenario.Content.Materialized.Single(m => m.Key.Contains("jei.toml")).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task O_diff_usa_o_manifesto_local_e_nunca_o_disco()
    {
        // ESTE é o guard. O conjunto gerenciado vem do manifesto que gravamos; um
        // arquivo do jogador (um mundo, um screenshot, o options.txt) jamais entra
        // no cálculo, porque ele nunca esteve no manifesto. Se algum dia alguém
        // trocar isto por uma varredura da pasta, o primeiro update apaga tudo.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var scenario = new Cenario(pack, version);

        var key = InstanceKey.New();
        scenario.Instances.Manifests[key] = new InstanceManifest
        {
            Schema = 1,
            ModpackId = pack.Id,
            ModpackVersionId = version.Id,
            ModpackName = pack.Name,
            Version = "1.0.0",
            InstalledAt = DateTimeOffset.UtcNow,

            // O manifesto anterior conhece um mod que saiu do pack. Só ele pode
            // ser apagado.
            ManagedFiles = new Dictionary<string, string> { ["mods/jei.jar"] = "aa", ["mods/velho.jar"] = "cc" }
        };

        await scenario.Instalar(key);

        scenario.Instances.Deleted.ShouldBe(["mods/velho.jar"]);
    }

    [Fact]
    public async Task Arquivo_intocado_nao_e_rebaixado_nem_rematerializado()
    {
        // O hash bate: não há trabalho a fazer. Sem isto, cada abertura do
        // launcher reinstalaria o pack inteiro.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var scenario = new Cenario(pack, version);
        scenario.Content.Hashes.Add("aa");

        var key = InstanceKey.New();
        scenario.Instances.Manifests[key] = Manifesto(
            pack, version, new Dictionary<string, string> { ["mods/jei.jar"] = "aa" });

        await scenario.Instalar(key);

        scenario.Downloader.Requested.ShouldBeEmpty();
        scenario.Content.Materialized.ShouldBeEmpty();
        scenario.Instances.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Arquivos_do_servidor_ficam_de_fora()
    {
        // O mesmo manifesto serve os dois lados. Baixar um mod server-only não
        // daria erro visível, mas custaria banda e disco à toa.
        var pack = Modpack();

        var version = Versao(
            pack.Id,
            Arquivo("mods/jei.jar", "aa"),
            Arquivo("mods/spark-server.jar", "bb", FileSide.ServerOnly));

        var scenario = new Cenario(pack, version);

        await scenario.Instalar();

        scenario.Downloader.Requested.ShouldBe(["aa"]);
    }

    [Fact]
    public async Task O_manifesto_gravado_descreve_o_estado_final_e_nao_o_trabalho_feito()
    {
        // Registrar só o que esta execução mexeu faria o diff seguinte achar que
        // os arquivos intocados são lixo — e apagá-los.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"), Arquivo("mods/rei.jar", "bb"));

        var scenario = new Cenario(pack, version);
        scenario.Content.Hashes.Add("aa");
        var key = InstanceKey.New();
        scenario.Instances.Manifests[key] = Manifesto(
            pack, version, new Dictionary<string, string> { ["mods/jei.jar"] = "aa" });

        await scenario.Instalar(key);

        var stored = scenario.Instances.Manifests[key];

        stored.ManagedFiles.Keys.ShouldBe(["mods/jei.jar", "mods/rei.jar"], true);
    }

    [Fact]
    public async Task A_ram_escolhida_pelo_jogador_sobrevive_a_atualizacao()
    {
        // Ela é dele, não do pack. Voltar para a recomendada a cada versão nova
        // desfaria em silêncio um ajuste que ele fez de propósito.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));
        var key = InstanceKey.New();

        var scenario = new Cenario(pack, version);
        scenario.Instances.Manifests[key] = Manifesto(pack, version, []) with { MemoryMb = 8192 };

        await scenario.Instalar(key);

        scenario.Instances.Manifests[key].MemoryMb.ShouldBe(8192);
    }

    [Fact]
    public async Task Modpack_sem_versao_publicada_explica_em_vez_de_falhar()
    {
        var pack = Modpack();
        var scenario = new Cenario(pack, null);

        var result = await scenario.InstalarUltima();

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("ainda não tem uma versão publicada");
    }

    [Fact]
    public async Task Falha_no_meio_vira_mensagem_e_o_manifesto_nao_e_gravado()
    {
        // Gravar um manifesto de uma instalação que não terminou faria o próximo
        // diff acreditar que os arquivos estão lá, e nada seria baixado de novo.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var scenario = new Cenario(pack, version);
        scenario.Connection.Throws = new InvalidOperationException("canal caiu");

        var result = await scenario.Instalar();

        result.Succeeded.ShouldBeFalse();
        scenario.Instances.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Downloads_correm_em_paralelo_ate_o_limite()
    {
        // Em série, um pack com milhares de arquivos pagava a latência de cada
        // pedido um atrás do outro — lento até em rede local.
        var pack = Modpack();
        var files = Enumerable.Range(0, 20).Select(i => Arquivo($"mods/m{i}.jar", $"h{i:00}")).ToArray();

        var scenario = new Cenario(pack, Versao(pack.Id, files))
        {
            Downloader = new FakeBlobDownloader { Delay = TimeSpan.FromMilliseconds(30) }
        };

        var result = await scenario.Instalar();

        result.Succeeded.ShouldBeTrue(result.Error);
        scenario.Content.Added.Count.ShouldBe(20);
        scenario.Downloader.MaxConcurrent.ShouldBeGreaterThan(1);
        scenario.Downloader.MaxConcurrent.ShouldBeLessThanOrEqualTo(InstallModpackVersion.ParallelDownloads);
    }

    [Fact]
    public async Task Mesmo_conteudo_em_dois_caminhos_baixa_uma_vez()
    {
        // Em paralelo, as duas cópias gravariam o mesmo temporário do store.
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("config/a.toml", "aa"), Arquivo("config/b.toml", "aa"));

        var scenario = new Cenario(pack, version);

        await scenario.Instalar();

        scenario.Downloader.Requested.ShouldBe(["aa"]);
        scenario.Content.Materialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task O_progresso_termina_em_Done()
    {
        var pack = Modpack();
        var version = Versao(pack.Id, Arquivo("mods/jei.jar", "aa"));

        var scenario = new Cenario(pack, version);
        var progress = new ProgressoSincrono<InstallProgress>();

        await scenario.Instalar(progress: progress);

        // Coletor síncrono, e não Progress<T>: aquele posta no contexto de
        // sincronização e a asserção corria antes da callback. Passava sozinho e
        // falhava com a suíte cheia — ou seja, nunca tinha verificado nada.
        progress.Relatado.Select(p => p.Phase).ShouldContain(InstallPhase.Done);
    }

    private static ModpackDto Modpack() => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = "pack",
        Name = "Pack",
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge
    };

    private static ModpackVersionDto Versao(Guid modpackId, params ModpackFileDto[] files) => new()
    {
        Id = Guid.CreateVersion7(),
        ModpackId = modpackId,
        Version = "1.2.0",
        LoaderVersion = "21.1.100",
        State = ModpackVersionState.Ready,
        PublishedAt = DateTimeOffset.UtcNow,
        RecommendedMemoryMb = 4096,
        Files = files
    };

    private static ModpackFileDto Arquivo(string path, string sha, FileSide side = FileSide.Both) => new()
    {
        Path = path, Sha256 = sha, SizeBytes = 10, Side = side
    };

    private static InstanceManifest Manifesto(
        ModpackDto pack, ModpackVersionDto version, Dictionary<string, string> files) => new()
    {
        Schema = 1,
        ModpackId = pack.Id,
        ModpackVersionId = version.Id,
        ModpackName = pack.Name,
        Version = version.Version,
        InstalledAt = DateTimeOffset.UtcNow,
        ManagedFiles = files
    };

    // ---------- apoio ----------

    private sealed class Cenario
    {
        public Cenario(ModpackDto pack, ModpackVersionDto? version)
        {
            Pack = pack;

            if (version is not null)
            {
                Connection.Versions[version.Id] = version;
                Connection.Latest[(pack.Id, ReleaseChannel.Release)] = version;
            }

            Versao = version;
        }

        public ModpackDto Pack { get; }

        public ModpackVersionDto? Versao { get; }

        public FakeServerConnection Connection { get; } = new();

        public FakeContentStore Content { get; } = new();

        public FakeBlobDownloader Downloader { get; init; } = new();

        public FakeInstanceStore Instances { get; } = new();

        private InstallModpackVersion Instalador => new(Connection, Content, Downloader, Instances);

        /// <summary>
        ///     Alvo explícito: é ele que distingue atualizar de duplicar. Os
        ///     casos que pré-semeiam uma instância passam a chave dela — sem
        ///     isso o instalador criaria uma instância nova ao lado e o teste
        ///     verificaria o diff contra uma pasta vazia, passando por engano.
        /// </summary>
        public Task<InstallResult> Instalar(
            InstanceKey? alvo = null,
            IProgress<InstallProgress>? progress = null) =>
            Instalador.HandleAsync(Servidor, Pack, Versao!.Id, alvo, progress, Ct);

        public Task<InstallResult> InstalarUltima() =>
            Instalador.InstallLatestAsync(Servidor, Pack, null, ReleaseChannel.Release, null, Ct);
    }
}
