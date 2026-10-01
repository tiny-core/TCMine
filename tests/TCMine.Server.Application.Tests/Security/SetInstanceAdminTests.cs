using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

public sealed class SetInstanceAdminTests
{
    [Fact]
    public async Task Promove_uma_conta()
    {
        var alvo = new User { MicrosoftObjectId = "oid", DisplayName = "Ana" };
        var users = new FakeUsers(alvo);

        var result = await new SetInstanceAdmin(users, new FakeUserScope(null) { IsInstanceAdmin = true })
            .HandleAsync(alvo.Id, true, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        alvo.IsInstanceAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task Nao_deixa_tirar_o_ultimo_admin()
    {
        var unico = new User { MicrosoftObjectId = "oid", DisplayName = "Ana", IsInstanceAdmin = true };
        var users = new FakeUsers(unico);

        var result = await new SetInstanceAdmin(users, new FakeUserScope(null) { IsInstanceAdmin = true })
            .HandleAsync(unico.Id, false, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        unico.IsInstanceAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task Rebaixar_um_entre_varios_admins_funciona()
    {
        var a = new User { MicrosoftObjectId = "oid-a", DisplayName = "A", IsInstanceAdmin = true };
        var b = new User { MicrosoftObjectId = "oid-b", DisplayName = "B", IsInstanceAdmin = true };
        var users = new FakeUsers(a, b);

        var result = await new SetInstanceAdmin(users, new FakeUserScope(null) { IsInstanceAdmin = true })
            .HandleAsync(a.Id, false, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        a.IsInstanceAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_pode_mudar_nada()
    {
        var alvo = new User { MicrosoftObjectId = "oid", DisplayName = "Ana" };
        var users = new FakeUsers(alvo);

        var result = await new SetInstanceAdmin(users, new FakeUserScope(null) { IsInstanceAdmin = false })
            .HandleAsync(alvo.Id, true, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        alvo.IsInstanceAdmin.ShouldBeFalse();
    }
}
