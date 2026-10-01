using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

public sealed class DenyAccessRequestTests
{
    [Fact]
    public async Task Recusar_marca_o_pedido_sem_criar_membership()
    {
        var pedido = new AccessRequest { UserId = Guid.CreateVersion7(), GameServerId = Guid.CreateVersion7() };

        var result = await new DenyAccessRequest(new FakeRequests(pedido), new FakeUserScope())
            .HandleAsync(pedido.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        pedido.Status.ShouldBe(AccessRequestStatus.Denied);
    }

    [Fact]
    public async Task Pedido_inexistente_falha()
    {
        var result = await new DenyAccessRequest(new FakeRequests(), new FakeUserScope())
            .HandleAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    private sealed class FakeRequests(params AccessRequest[] seed) : FakeAccessRequestRepositoryBase
    {
        public override Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(seed.FirstOrDefault(r => r.Id == id));

        public override Task UpdateAsync(AccessRequest request, CancellationToken ct) => Task.CompletedTask;
    }
}
