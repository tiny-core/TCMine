using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     O jogo aberto e o que ele escreveu.
///     Duas regras aqui não são cosméticas: não deixar abrir uma segunda cópia
///     (duas escrevendo o mesmo mundo corrompem-no) e limitar o log (um modpack
///     grande escreve dezenas de milhares de linhas no arranque).
/// </summary>
public class GameSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Enquanto_ha_jogo_aberto_o_segundo_attach_e_recusado()
    {
        var sessao = new GameSession();
        var primeiro = new FakeGameProcess();

        sessao.Attach(Instalada(), primeiro).ShouldBeTrue();
        sessao.Attach(Instalada(), new FakeGameProcess()).ShouldBeFalse();
        sessao.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public async Task Quando_o_jogo_fecha_a_sessao_limpa_mas_guarda_o_codigo()
    {
        // Um código diferente de zero é a única pista de que o jogo não fechou
        // sozinho. Apagá-lo no fecho tiraria a informação quando ela nasce.
        var sessao = new GameSession();
        var processo = new FakeGameProcess { CodigoDeSaida = 1 };

        sessao.Attach(Instalada(), processo);
        processo.Terminar();

        await EsperarAsync(() => !sessao.IsRunning);

        sessao.Running.ShouldBeNull();
        sessao.LastExitCode.ShouldBe(1);
    }

    [Fact]
    public async Task Depois_de_fechar_da_para_abrir_outra_vez()
    {
        var sessao = new GameSession();
        var primeiro = new FakeGameProcess();

        sessao.Attach(Instalada(), primeiro);
        primeiro.Terminar();

        await EsperarAsync(() => !sessao.IsRunning);

        sessao.Attach(Instalada(), new FakeGameProcess()).ShouldBeTrue();
    }

    [Fact]
    public async Task O_log_guarda_as_ultimas_linhas_e_descarta_as_velhas()
    {
        // As antigas caem pela frente, que é o lado certo: num crash o que
        // interessa é o fim.
        var sessao = new GameSession();
        var processo = new FakeGameProcess();

        sessao.Attach(Instalada(), processo);

        for (var i = 0; i < GameSession.MaxLogLines + 50; i++)
            processo.Escrever($"linha {i}");

        processo.Terminar();

        await EsperarAsync(() => !sessao.IsRunning);

        sessao.Log.Count.ShouldBe(GameSession.MaxLogLines);
        sessao.Log[^1].ShouldBe($"linha {GameSession.MaxLogLines + 49}");
        sessao.Log[0].ShouldBe("linha 50");
    }

    [Fact]
    public async Task Falha_ao_ler_o_log_nao_deixa_a_sessao_presa()
    {
        // O jogo continua a correr; perder o launcher por causa de um pipe seria
        // pior do que perder o log. E ficar "a jogar" para sempre impediria
        // qualquer abertura seguinte.
        var sessao = new GameSession();
        var processo = new FakeGameProcess { Erro = new IOException("pipe fechado") };

        sessao.Attach(Instalada(), processo);
        processo.Escrever("antes do erro");

        await EsperarAsync(() => !sessao.IsRunning);

        sessao.IsRunning.ShouldBeFalse();
        sessao.Log.ShouldContain(l => l.Contains("pipe fechado", StringComparison.Ordinal));
    }

    [Fact]
    public void Matar_sem_jogo_aberto_nao_estoura()
    {
        // A tela pode pedir isto numa corrida com o fecho do próprio jogo.
        Should.NotThrow(() => new GameSession().Kill());
    }

    // ---------- apoio ----------

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        // A bomba corre em background: a asserção tem de esperar por ela, e um
        // laço curto com desistência é melhor do que um Delay a adivinhar.
        for (var i = 0; i < 200 && !condicao(); i++)
            await Task.Delay(10, Ct);

        condicao().ShouldBeTrue("a sessão não chegou ao estado esperado a tempo");
    }

    private static InstalledInstance Instalada()
    {
        var chave = new InstanceKey(Guid.CreateVersion7(), Guid.CreateVersion7());

        return new InstalledInstance(
            chave,
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = chave.ModpackId,
                ModpackVersionId = chave.ModpackVersionId,
                ModpackName = "Pack",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>(),
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge
            },
            SizeBytes: 0,
            Path: "/instancias/pack");
    }

}
