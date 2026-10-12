using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Application.Updates;

namespace TCMine.Server.Application.Tests.Updates;

/// <summary>O aviso de versão nova aparece quando, e só quando, há o que atualizar.</summary>
public sealed class CheckServerUpdateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("0.4.0", "0.5.0", true)]
    [InlineData("0.5.0", "0.5.0", false)]
    [InlineData("0.6.0", "0.5.0", false)]
    [InlineData("0.5.0-beta.1", "0.5.0", true)] // a estável sai DEPOIS da beta de mesmo número
    [InlineData("0.9.0", "0.10.0", true)] // numérico, não alfabético
    [InlineData("dev", "0.5.0", false)] // build local não se compara
    public async Task Avisa_so_quando_a_publicada_e_maior(string current, string published, bool expected)
    {
        var caso = Admin(Feed.Publicada(published));

        var result = await caso.HandleAsync(current, Ct);

        (result is not null).ShouldBe(expected);
    }

    [Fact]
    public async Task Quem_nao_e_admin_da_instalacao_nao_ve()
    {
        var caso = new CheckServerUpdate(Feed.Publicada("9.9.9"), new FakeUserScope { IsInstanceAdmin = false });

        (await caso.HandleAsync("0.1.0", Ct)).ShouldBeNull();
        (await caso.CheckAsync("0.1.0", refresh: true, Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("0.4.0", ServerUpdateState.Available)]
    [InlineData("0.5.0", ServerUpdateState.UpToDate)]
    [InlineData("dev", ServerUpdateState.LocalBuild)]
    public async Task Verificar_diz_o_estado_e_mostra_a_publicada(string current, ServerUpdateState expected)
    {
        var caso = Admin(Feed.Publicada("0.5.0"));

        var status = await caso.CheckAsync(current, refresh: true, Ct);

        status.ShouldNotBeNull();
        status.State.ShouldBe(expected);
        status.Latest.ShouldNotBeNull().Version.ShouldBe("0.5.0");
    }

    [Fact]
    public async Task GitHub_fora_do_ar_nao_vira_esta_atualizado()
    {
        // Dizer "está na mais recente" sem ter conseguido perguntar seria
        // exatamente a falsa calma que este aviso existe para evitar.
        var caso = Admin(new Feed(new ServerReleaseLookup(ReleaseFeedState.Unreachable, null, DateTimeOffset.UtcNow)));

        (await caso.CheckAsync("0.4.0", refresh: true, Ct))!.State.ShouldBe(ServerUpdateState.Unreachable);
        (await caso.HandleAsync("0.4.0", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Consulta_desligada_e_dita_como_desligada()
    {
        var caso = Admin(new Feed(new ServerReleaseLookup(ReleaseFeedState.Disabled, null, DateTimeOffset.UtcNow)));

        (await caso.CheckAsync("0.4.0", refresh: true, Ct))!.State.ShouldBe(ServerUpdateState.Disabled);
    }

    [Fact]
    public async Task So_o_verificar_agora_pede_consulta_nova()
    {
        var feed = Feed.Publicada("0.5.0");
        var caso = Admin(feed);

        await caso.HandleAsync("0.4.0", Ct);
        feed.UltimoRefresh.ShouldBe(false);

        await caso.CheckAsync("0.4.0", refresh: true, Ct);
        feed.UltimoRefresh.ShouldBe(true);
    }

    private static CheckServerUpdate Admin(IServerReleaseFeed feed) =>
        new(feed, new FakeUserScope { IsInstanceAdmin = true });

    private sealed class Feed(ServerReleaseLookup lookup) : IServerReleaseFeed
    {
        public bool? UltimoRefresh { get; private set; }

        public static Feed Publicada(string version) =>
            new(new ServerReleaseLookup(
                ReleaseFeedState.Ok,
                new ServerRelease(version, new Uri("https://github.com/x/y/releases"), DateTimeOffset.UtcNow),
                DateTimeOffset.UtcNow));

        public Task<ServerReleaseLookup> LookupAsync(bool refresh, CancellationToken ct)
        {
            UltimoRefresh = refresh;
            return Task.FromResult(lookup);
        }
    }
}
