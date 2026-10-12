using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     Por onde entrar. De dentro de casa o endereço público costuma falhar
///     (roteador sem "NAT loopback"); de fora, o local não responde. A escolha
///     tem de acertar os dois sem perguntar nada ao jogador.
/// </summary>
public sealed class ServerRouteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ServerAddress Local = new("192.168.1.100", 25566);

    [Fact]
    public async Task Em_casa_entra_pela_rede_local()
    {
        var route = await ServerRoute.ChooseAsync(Servidor(), new FakeEndpointProbe(Local), Ct);

        route.ShouldBe(Local);
    }

    [Fact]
    public async Task Fora_de_casa_o_local_nao_responde_e_entra_pelo_publico()
    {
        var route = await ServerRoute.ChooseAsync(Servidor(), new FakeEndpointProbe(), Ct);

        route.ShouldBe(new ServerAddress("jogar.exemplo.com", null));
    }

    [Fact]
    public async Task Sem_endereco_local_nem_sonda()
    {
        // Quem não informou o IP local não paga o prazo da sonda.
        var probe = new FakeEndpointProbe();

        var route = await ServerRoute.ChooseAsync(Servidor() with { LocalAddress = null }, probe, Ct);

        route.ShouldBe(new ServerAddress("jogar.exemplo.com", null));
        probe.Probed.ShouldBeEmpty();
    }

    [Fact]
    public async Task So_com_endereco_local_usa_ele_mesmo_sem_resposta()
    {
        // É o único caminho que existe: melhor o jogo tentar e dizer o que
        // houve do que o launcher recusar sem explicar.
        var route = await ServerRoute.ChooseAsync(
            Servidor() with { ConnectAddress = null }, new FakeEndpointProbe(), Ct);

        route.ShouldBe(Local);
    }

    private static GameServerDto Servidor() => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "ATM10",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogar.exemplo.com",
        LocalAddress = "192.168.1.100:25566",
        Status = GameServerStatus.Running,
        Role = ServerRoleDto.Member,
        AccessState = ServerAccessState.Granted
    };
}
