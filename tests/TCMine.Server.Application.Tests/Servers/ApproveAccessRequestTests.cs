using TCMine.Contracts.Servers;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

public sealed class ApproveAccessRequestTests
{
    [Fact]
    public async Task Aprovar_cria_membership_e_sincroniza_a_whitelist()
    {
        var player = Guid.CreateVersion7();
        var gameServerId = Guid.CreateVersion7();
        var pedido = new AccessRequest { UserId = player, GameServerId = gameServerId };
        var memberships = new FakeMemberships();
        var whitelist = new FakeWhitelistSync();

        var result = await new ApproveAccessRequest(
                new FakeRequests(pedido), memberships, whitelist, new FakeUserScope())
            .HandleAsync(pedido.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        memberships.Adicionado.ShouldNotBeNull();
        memberships.Adicionado!.UserId.ShouldBe(player);
        memberships.Adicionado.Role.ShouldBe(ServerRole.Member);
        whitelist.Sincronizados.ShouldContain(gameServerId);
    }

    [Fact]
    public async Task So_admin_ou_owner_do_servidor_pode_decidir()
    {
        var pedido = new AccessRequest { UserId = Guid.CreateVersion7(), GameServerId = Guid.CreateVersion7() };

        // Moderator (abaixo de Admin) não decide quem entra.
        var result = await new ApproveAccessRequest(
                new FakeRequests(pedido), new FakeMemberships(), new FakeWhitelistSync(),
                new FakeUserScope(ServerRoleDto.Moderator))
            .HandleAsync(pedido.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Pedido_ja_resolvido_nao_pode_ser_aprovado_de_novo()
    {
        var pedido = new AccessRequest { UserId = Guid.CreateVersion7(), GameServerId = Guid.CreateVersion7() };
        pedido.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var result = await new ApproveAccessRequest(
                new FakeRequests(pedido), new FakeMemberships(), new FakeWhitelistSync(), new FakeUserScope())
            .HandleAsync(pedido.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    private sealed class FakeRequests(params AccessRequest[] seed) : FakeAccessRequestRepositoryBase
    {
        public override Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(seed.FirstOrDefault(r => r.Id == id));

        public override Task UpdateAsync(AccessRequest request, CancellationToken ct) => Task.CompletedTask;
    }
}
