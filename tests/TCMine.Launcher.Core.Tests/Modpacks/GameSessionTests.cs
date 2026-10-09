using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

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
        var session = new GameSession();
        var primeiro = new FakeGameProcess();

        session.Attach(Instalada(), primeiro).ShouldBeTrue();
        session.Attach(Instalada(), new FakeGameProcess()).ShouldBeFalse();
        session.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public async Task Quando_o_jogo_fecha_a_sessao_limpa_mas_guarda_o_codigo()
    {
        // Um código diferente de zero é a única pista de que o jogo não fechou
        // sozinho. Apagá-lo no fecho tiraria a informação quando ela nasce.
        var session = new GameSession();
        var process = new FakeGameProcess { CodigoDeSaida = 1 };

        session.Attach(Instalada(), process);
        process.Terminar();

        await EsperarAsync(() => !session.IsRunning);

        session.Running.ShouldBeNull();
        session.LastExitCode.ShouldBe(1);
    }

    [Fact]
    public async Task Depois_de_fechar_da_para_abrir_outra_vez()
    {
        var session = new GameSession();
        var primeiro = new FakeGameProcess();

        session.Attach(Instalada(), primeiro);
        primeiro.Terminar();

        await EsperarAsync(() => !session.IsRunning);

        session.Attach(Instalada(), new FakeGameProcess()).ShouldBeTrue();
    }

    [Fact]
    public async Task O_log_guarda_as_ultimas_linhas_e_descarta_as_velhas()
    {
        // As antigas caem pela frente, que é o lado certo: num crash o que
        // interessa é o fim.
        var session = new GameSession();
        var process = new FakeGameProcess();

        session.Attach(Instalada(), process);

        for (var i = 0; i < GameSession.MaxLogLines + 50; i++)
            process.Escrever($"linha {i}");

        process.Terminar();

        await EsperarAsync(() => !session.IsRunning);

        session.Log.Count.ShouldBe(GameSession.MaxLogLines);
        session.Log[^1].ShouldBe($"linha {GameSession.MaxLogLines + 49}");
        session.Log[0].ShouldBe("linha 50");
    }

    [Fact]
    public async Task Falha_ao_ler_o_log_nao_deixa_a_sessao_presa()
    {
        // O jogo continua a correr; perder o launcher por causa de um pipe seria
        // pior do que perder o log. E ficar "a jogar" para sempre impediria
        // qualquer abertura seguinte.
        var session = new GameSession();
        var process = new FakeGameProcess { Erro = new IOException("pipe fechado") };

        session.Attach(Instalada(), process);
        process.Escrever("antes do erro");

        await EsperarAsync(() => !session.IsRunning);

        session.IsRunning.ShouldBeFalse();
        session.Log.ShouldContain(l => l.Contains("pipe fechado", StringComparison.Ordinal));
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
        var key = InstanceKey.New();

        return new InstalledInstance(
            key,
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = Guid.CreateVersion7(),
                ModpackVersionId = Guid.CreateVersion7(),
                ModpackName = "Pack",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>(),
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge
            },
            0,
            "/instancias/pack");
    }
}
