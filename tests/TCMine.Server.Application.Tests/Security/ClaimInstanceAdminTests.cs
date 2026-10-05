using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Resgate da administração quando nenhum admin consegue entrar pela
///     Microsoft (contas de e-mail e senha de antes). A prova é o código do log,
///     nunca "quem entrou primeiro".
/// </summary>
public sealed class ClaimInstanceAdminTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Codigo_certo_promove_e_funde_o_admin_antigo()
    {
        var antigo = new User { DisplayName = "admin", IsInstanceAdmin = true, MinecraftUuid = "uuid-admin" };
        var novo = new User { DisplayName = "Admin", MicrosoftObjectId = "oid" };
        var users = new FakeUsers(antigo, novo);
        var codes = new AdminClaimCode();
        var code = codes.Issue();

        var result = await new ClaimInstanceAdmin(users, codes).HandleAsync(novo.Id, code, Ct);

        result.Succeeded.ShouldBeTrue();
        novo.IsInstanceAdmin.ShouldBeTrue();
        users.Fusoes.ShouldBe([(novo.Id, antigo.Id)]);
        novo.MinecraftUuid.ShouldBe("uuid-admin");
    }

    [Fact]
    public async Task Codigo_errado_nao_promove()
    {
        var novo = new User { DisplayName = "Alguém", MicrosoftObjectId = "oid" };
        var users = new FakeUsers(new User { DisplayName = "admin", IsInstanceAdmin = true }, novo);
        var codes = new AdminClaimCode();
        codes.Issue();

        var result = await new ClaimInstanceAdmin(users, codes).HandleAsync(novo.Id, "AAAA-BBBB-CCCC-DDDD", Ct);

        result.Succeeded.ShouldBeFalse();
        novo.IsInstanceAdmin.ShouldBeFalse();
    }

    [Fact]
    public void Codigo_vale_uma_vez_e_ignora_caixa_e_hifens()
    {
        var codes = new AdminClaimCode();
        var code = codes.Issue();

        codes.TryConsume(code.Replace("-", "").ToLowerInvariant()).ShouldBeTrue();
        codes.TryConsume(code).ShouldBeFalse();
    }

    [Fact]
    public async Task Com_admin_que_entra_pela_microsoft_o_resgate_nao_existe()
    {
        var admin = new User { DisplayName = "Admin", MicrosoftObjectId = "oid-admin", IsInstanceAdmin = true };
        var outro = new User { DisplayName = "Outro", MicrosoftObjectId = "oid-outro" };
        var users = new FakeUsers(admin, outro);
        var codes = new AdminClaimCode();
        var code = codes.Issue();
        var claim = new ClaimInstanceAdmin(users, codes);

        (await claim.IsNeededAsync(Ct)).ShouldBeFalse();
        (await claim.HandleAsync(outro.Id, code, Ct)).Succeeded.ShouldBeFalse();
        outro.IsInstanceAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task Varios_admins_antigos_nao_sao_fundidos_as_cegas()
    {
        var users = new FakeUsers(
            new User { DisplayName = "a", IsInstanceAdmin = true },
            new User { DisplayName = "b", IsInstanceAdmin = true });
        var novo = new User { DisplayName = "Novo", MicrosoftObjectId = "oid" };
        await users.AddAsync(novo, Ct);
        var codes = new AdminClaimCode();

        var result = await new ClaimInstanceAdmin(users, codes).HandleAsync(novo.Id, codes.Issue(), Ct);

        result.Succeeded.ShouldBeTrue();
        novo.IsInstanceAdmin.ShouldBeTrue();
        users.Fusoes.ShouldBeEmpty();
    }
}
