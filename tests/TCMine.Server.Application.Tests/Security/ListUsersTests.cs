using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

public sealed class ListUsersTests
{
    [Fact]
    public async Task So_admin_da_instalacao_ve_a_lista()
    {
        var result = await new ListUsers(new FakeUsers(), new FakeUserScope(null) { IsInstanceAdmin = false })
            .HandleAsync(TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Admin_ve_todas_as_contas()
    {
        var a = new User { MicrosoftObjectId = "oid-a", DisplayName = "A" };
        var b = new User { MinecraftUuid = "uuid-b", DisplayName = "B" };

        var result = await new ListUsers(new FakeUsers(a, b), new FakeUserScope(null) { IsInstanceAdmin = true })
            .HandleAsync(TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Value!.Count.ShouldBe(2);
    }
}
