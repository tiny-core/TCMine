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
    [InlineData("0.9.0", "0.10.0", true)]       // numérico, não alfabético
    [InlineData("dev", "0.5.0", false)]         // build local não se compara
    public async Task Avisa_so_quando_a_publicada_e_maior(string current, string published, bool expected)
    {
        var caso = new CheckServerUpdate(new Feed(published), new FakeUserScope { IsInstanceAdmin = true });

        var result = await caso.HandleAsync(current, Ct);

        (result is not null).ShouldBe(expected);
    }

    [Fact]
    public async Task Quem_nao_e_admin_da_instalacao_nao_ve()
    {
        var caso = new CheckServerUpdate(new Feed("9.9.9"), new FakeUserScope { IsInstanceAdmin = false });

        (await caso.HandleAsync("0.1.0", Ct)).ShouldBeNull();
    }

    private sealed class Feed(string version) : IServerReleaseFeed
    {
        public Task<ServerRelease?> GetLatestStableAsync(CancellationToken ct) =>
            Task.FromResult<ServerRelease?>(
                new ServerRelease(version, new Uri("https://github.com/x/y/releases"), DateTimeOffset.UtcNow));
    }
}
