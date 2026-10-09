using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     Cada admin só vê os pedidos dos SEUS servidores — resolvido pelo mesmo
///     Membership que já existe, sem nenhuma tabela nova de dono.
/// </summary>
public sealed class ListAccessRequestsTests
{
    [Fact]
    public async Task Admin_nao_ve_pedido_de_servidor_de_outro()
    {
        var admin = Guid.CreateVersion7();
        var meuServidor = Servidor("Meu");
        var alheio = Servidor("Alheio");

        var meuPedido = Pedido(meuServidor.Id);
        var pedidoAlheio = Pedido(alheio.Id);

        var lista = await new ListAccessRequests(
                new FakeServers(meuServidor, alheio),
                new FakeMemberships(new Membership
                {
                    UserId = admin, GameServerId = meuServidor.Id, Role = ServerRole.Admin
                }),
                new FakeRequests(meuPedido, pedidoAlheio),
                new FakeUserScope(null) { UserId = admin })
            .HandleAsync(TestContext.Current.CancellationToken);

        var unico = lista.ShouldHaveSingleItem();
        unico.GameServerId.ShouldBe(meuServidor.Id);
    }

    [Fact]
    public async Task Moderador_nao_decide_entao_nao_ve_pedidos()
    {
        // Moderator modera o jogo (kick, ban, console); decidir quem entra é
        // nível de Admin para cima — a mesma régua de AccessRequestPolicy.
        var moderador = Guid.CreateVersion7();
        var servidor = Servidor("Servidor");

        var lista = await new ListAccessRequests(
                new FakeServers(servidor),
                new FakeMemberships(new Membership
                {
                    UserId = moderador, GameServerId = servidor.Id, Role = ServerRole.Moderator
                }),
                new FakeRequests(Pedido(servidor.Id)),
                new FakeUserScope(null) { UserId = moderador })
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.ShouldBeEmpty();
    }

    [Fact]
    public async Task Admin_da_instalacao_ve_pedidos_de_todos_os_servidores()
    {
        var meuServidor = Servidor("Meu");
        var alheio = Servidor("Alheio");

        var lista = await new ListAccessRequests(
                new FakeServers(meuServidor, alheio),
                new FakeMemberships(),
                new FakeRequests(Pedido(meuServidor.Id), Pedido(alheio.Id)),
                new FakeUserScope(null) { UserId = Guid.CreateVersion7(), IsInstanceAdmin = true })
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.Count.ShouldBe(2);
    }

    private static GameServer Servidor(string name) => new()
    {
        Name = name,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = $"{name.ToLowerInvariant()}:25565",
        RconSecret = "segredo"
    };

    private static AccessRequest Pedido(Guid gameServerId) =>
        new() { UserId = Guid.CreateVersion7(), GameServerId = gameServerId };

    private sealed class FakeServers(params GameServer[] seed) : FakeServerRepositoryBase
    {
        public override Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>(seed);
    }

    private sealed class FakeRequests(params AccessRequest[] seed) : FakeAccessRequestRepositoryBase
    {
        public override Task<IReadOnlyList<AccessRequestView>> ListPendingForServersAsync(
            IReadOnlyList<Guid> gameServerIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccessRequestView>>(
            [
                .. seed
                    .Where(r => gameServerIds.Contains(r.GameServerId))
                    .Select(r =>
                        new AccessRequestView(r.Id, r.UserId, "jogador", r.GameServerId, "servidor", r.CreatedAt))
            ]);
    }
}
