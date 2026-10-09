using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     Saber que há versão nova.
///     A tela marcava "Instalado" comparando só o id do modpack e ignorando a
///     versão, então o administrador publicava a 1.1 e o jogador nunca ficava a
///     saber. A promessa do produto — o servidor publica, o launcher reconcilia —
///     não tinha gatilho na interface.
/// </summary>
public class CheckInstanceUpdatesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Instancia_numa_versao_antiga_aparece()
    {
        var pack = Guid.CreateVersion7();
        var nova = Versao(pack);
        var instance = Instalada(pack, Guid.CreateVersion7());

        var news = await Build((pack, nova)).HandleAsync([instance], Ct);

        news[instance.Key].Id.ShouldBe(nova.Id);
    }

    [Fact]
    public async Task Instancia_em_dia_nao_aparece()
    {
        var pack = Guid.CreateVersion7();
        var atual = Versao(pack);

        var news = await Build((pack, atual)).HandleAsync([Instalada(pack, atual.Id)], Ct);

        news.ShouldBeEmpty();
    }

    [Fact]
    public async Task Duas_instancias_do_mesmo_pack_custam_uma_consulta()
    {
        // Duas instalações do mesmo pack passaram a ser possíveis, e perguntar a
        // mesma coisa duas vezes seria desperdício no arranque de uma tela.
        var pack = Guid.CreateVersion7();
        var nova = Versao(pack);
        var connection = new FakeServerConnection { Latest = { [(pack, ReleaseChannel.Release)] = nova } };

        var news = await new CheckInstanceUpdates(connection).HandleAsync(
            [Instalada(pack, Guid.CreateVersion7()), Instalada(pack, Guid.CreateVersion7())], Ct);

        news.Count.ShouldBe(2);
        connection.LatestQueries.ShouldBe([(pack, ReleaseChannel.Release)]);
    }

    [Fact]
    public async Task Canal_em_baixo_nao_inventa_novidade_nem_estoura()
    {
        // "Não saber" não é "há novidade": botões que falham ao clicar seriam
        // piores do que nenhum, e esta consulta é um extra sobre uma tela que
        // tem de servir offline.
        var connection = new FakeServerConnection { Throws = new InvalidOperationException("canal fechado") };

        var news = await new CheckInstanceUpdates(connection).HandleAsync(
            [Instalada(Guid.CreateVersion7(), Guid.CreateVersion7())], Ct);

        news.ShouldBeEmpty();
    }

    [Fact]
    public async Task Uma_instancia_alpha_so_olha_para_o_canal_alpha()
    {
        // O essencial do canal. Uma alpha que recebesse a estável saltaria para
        // trás sem ninguém pedir, e no caminho reescreveria os mods por cima de
        // um mundo jogado em código de teste.
        var pack = Guid.CreateVersion7();
        var alphaNova = Versao(pack, "1.2.0-beta2");
        var connection = new FakeServerConnection
        {
            Latest = { [(pack, ReleaseChannel.Release)] = Versao(pack), [(pack, ReleaseChannel.Alpha)] = alphaNova }
        };

        var instance = Instalada(pack, Guid.CreateVersion7(), "1.2.0-beta1");

        var news = await new CheckInstanceUpdates(connection).HandleAsync([instance], Ct);

        news[instance.Key].Id.ShouldBe(alphaNova.Id);
        connection.LatestQueries.ShouldBe([(pack, ReleaseChannel.Alpha)]);
    }

    [Fact]
    public async Task Uma_estavel_nunca_recebe_alpha()
    {
        var pack = Guid.CreateVersion7();
        var connection = new FakeServerConnection
        {
            Latest = { [(pack, ReleaseChannel.Alpha)] = Versao(pack, "2.0.0-beta") }
        };

        // Só há alpha publicada. A estável fica onde está em vez de saltar de canal.
        var news = await new CheckInstanceUpdates(connection).HandleAsync(
            [Instalada(pack, Guid.CreateVersion7())], Ct);

        news.ShouldBeEmpty();
        connection.LatestQueries.ShouldBe([(pack, ReleaseChannel.Release)]);
    }

    [Fact]
    public async Task Uma_alpha_e_uma_estavel_do_mesmo_pack_sao_duas_consultas()
    {
        // Agrupar só por modpack faria uma das duas receber a resposta da outra.
        var pack = Guid.CreateVersion7();
        var connection = new FakeServerConnection();

        await new CheckInstanceUpdates(connection).HandleAsync(
            [
                Instalada(pack, Guid.CreateVersion7()),
                Instalada(pack, Guid.CreateVersion7(), "1.1.0-beta")
            ],
            Ct);

        connection.LatestQueries.ShouldBe(
            [(pack, ReleaseChannel.Release), (pack, ReleaseChannel.Alpha)], true);
    }

    // ---------- apoio ----------

    private static CheckInstanceUpdates Build(params (Guid Pack, ModpackVersionDto Versao)[] ultimas)
    {
        var connection = new FakeServerConnection();

        foreach (var (pack, version) in ultimas)
            connection.Latest[(pack, ReleaseChannel.Release)] = version;

        return new CheckInstanceUpdates(connection);
    }

    private static ModpackVersionDto Versao(Guid packId, string numero = "1.1.0") => new()
    {
        Id = Guid.CreateVersion7(),
        ModpackId = packId,
        Version = numero,
        LoaderVersion = "21.1.0",
        State = ModpackVersionState.Ready,
        PublishedAt = DateTimeOffset.UtcNow,
        Files = []
    };

    private static InstalledInstance Instalada(Guid packId, Guid versaoId, string numero = "1.0.0") =>
        new(InstanceKey.New(),
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = packId,
                ModpackVersionId = versaoId,
                ModpackName = "Pack",
                Version = numero,
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>()
            },
            0,
            "/instancias/pack");
}
