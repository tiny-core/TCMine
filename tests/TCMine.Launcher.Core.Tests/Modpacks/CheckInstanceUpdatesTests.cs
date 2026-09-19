using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;
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
        var instancia = Instalada(pack, Guid.CreateVersion7());

        var novidades = await Montar((pack, nova)).HandleAsync([instancia], Ct);

        novidades[instancia.Key].Id.ShouldBe(nova.Id);
    }

    [Fact]
    public async Task Instancia_em_dia_nao_aparece()
    {
        var pack = Guid.CreateVersion7();
        var atual = Versao(pack);

        var novidades = await Montar((pack, atual)).HandleAsync([Instalada(pack, atual.Id)], Ct);

        novidades.ShouldBeEmpty();
    }

    [Fact]
    public async Task Duas_instancias_do_mesmo_pack_custam_uma_consulta()
    {
        // Duas instalações do mesmo pack passaram a ser possíveis, e perguntar a
        // mesma coisa duas vezes seria desperdício no arranque de uma tela.
        var pack = Guid.CreateVersion7();
        var nova = Versao(pack);
        var conexao = new FakeServerConnection { Latest = { [pack] = nova } };

        var novidades = await new CheckInstanceUpdates(conexao).HandleAsync(
            [Instalada(pack, Guid.CreateVersion7()), Instalada(pack, Guid.CreateVersion7())], Ct);

        novidades.Count.ShouldBe(2);
        conexao.LatestQueries.ShouldBe([pack]);
    }

    [Fact]
    public async Task Canal_em_baixo_nao_inventa_novidade_nem_estoura()
    {
        // "Não saber" não é "há novidade": botões que falham ao clicar seriam
        // piores do que nenhum, e esta consulta é um extra sobre uma tela que
        // tem de servir offline.
        var conexao = new FakeServerConnection { Throws = new InvalidOperationException("canal fechado") };

        var novidades = await new CheckInstanceUpdates(conexao).HandleAsync(
            [Instalada(Guid.CreateVersion7(), Guid.CreateVersion7())], Ct);

        novidades.ShouldBeEmpty();
    }

    // ---------- apoio ----------

    private static CheckInstanceUpdates Montar(params (Guid Pack, ModpackVersionDto Versao)[] ultimas)
    {
        var conexao = new FakeServerConnection();

        foreach (var (pack, versao) in ultimas)
            conexao.Latest[pack] = versao;

        return new CheckInstanceUpdates(conexao);
    }

    private static ModpackVersionDto Versao(Guid packId) => new()
    {
        Id = Guid.CreateVersion7(),
        ModpackId = packId,
        Version = "1.1.0",
        LoaderVersion = "21.1.0",
        State = ModpackVersionState.Ready,
        PublishedAt = DateTimeOffset.UtcNow,
        Files = []
    };

    private static InstalledInstance Instalada(Guid packId, Guid versaoId) =>
        new(InstanceKey.New(),
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = packId,
                ModpackVersionId = versaoId,
                ModpackName = "Pack",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>()
            },
            SizeBytes: 0,
            Path: "/instancias/pack");

}
