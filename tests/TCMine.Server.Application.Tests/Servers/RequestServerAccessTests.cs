using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

public sealed class RequestServerAccessTests
{
    [Fact]
    public async Task Cria_o_pedido()
    {
        var player = Guid.CreateVersion7();
        var servidor = Servidor();
        var requests = new FakeRequests();

        var result = await new RequestServerAccess(
                new FakeServers(servidor), new FakeMemberships(), requests, new FakeUserScope(null) { UserId = player })
            .HandleAsync(servidor.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        requests.Adicionado.ShouldNotBeNull();
        requests.Adicionado!.UserId.ShouldBe(player);
        requests.Adicionado.GameServerId.ShouldBe(servidor.Id);
    }

    [Fact]
    public async Task Pedir_duas_vezes_nao_duplica()
    {
        var player = Guid.CreateVersion7();
        var servidor = Servidor();
        var pendente = new AccessRequest { UserId = player, GameServerId = servidor.Id };
        var requests = new FakeRequests(pendente);

        var result = await new RequestServerAccess(
                new FakeServers(servidor), new FakeMemberships(), requests, new FakeUserScope(null) { UserId = player })
            .HandleAsync(servidor.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();

        // Nada novo: o pedido que já existia é a resposta.
        requests.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Recusa_se_ja_tem_acesso()
    {
        var player = Guid.CreateVersion7();
        var servidor = Servidor();
        var membership = new Membership { UserId = player, GameServerId = servidor.Id, Role = ServerRole.Member };

        var result = await new RequestServerAccess(
                new FakeServers(servidor), new FakeMemberships(membership), new FakeRequests(),
                new FakeUserScope(null) { UserId = player })
            .HandleAsync(servidor.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Recusa_servidor_inexistente()
    {
        var result = await new RequestServerAccess(
                new FakeServers(), new FakeMemberships(), new FakeRequests(),
                new FakeUserScope(null) { UserId = Guid.CreateVersion7() })
            .HandleAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    private static GameServer Servidor() => new()
    {
        Name = "Servidor",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "servidor:25565",
        RconSecret = "segredo"
    };

    private sealed class FakeServers(params GameServer[] seed) : FakeServerRepositoryBase
    {
        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(seed.FirstOrDefault(s => s.Id == id));
    }

    private sealed class FakeRequests(params AccessRequest[] seed) : FakeAccessRequestRepositoryBase
    {
        private readonly List<AccessRequest> _requests = [.. seed];

        public AccessRequest? Adicionado { get; private set; }

        public override Task AddAsync(AccessRequest request, CancellationToken ct)
        {
            Adicionado = request;
            _requests.Add(request);
            return Task.CompletedTask;
        }

        public override Task<AccessRequest?> GetPendingAsync(Guid userId, Guid gameServerId, CancellationToken ct) =>
            Task.FromResult(_requests.FirstOrDefault(r => r.UserId == userId && r.GameServerId == gameServerId));
    }
}
