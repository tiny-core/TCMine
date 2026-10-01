using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Vincular Minecraft a uma conta que já existe — o caminho de quem entrou
///     no painel só com a Microsoft e um dia compra o jogo.
/// </summary>
public sealed class LinkMinecraftAccountTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vincula_o_uuid_a_conta_logada()
    {
        var user = new User { MicrosoftObjectId = "oid", DisplayName = "Ana" };
        var users = new FakeUsers(user);
        var caso = new LinkMinecraftAccount(new FakeExchange("mc-token"), new FakeProfiles("uuid-ana"), users);

        var result = await caso.HandleAsync(user.Id, "ms-token", Ct);

        result.Succeeded.ShouldBeTrue();
        user.MinecraftUuid.ShouldBe("uuid-ana");
    }

    [Fact]
    public async Task Recusa_quando_o_minecraft_ja_pertence_a_outra_conta()
    {
        var dono = new User { MicrosoftObjectId = "oid-dono", DisplayName = "Dono", MinecraftUuid = "uuid-disputado" };
        var pretendente = new User { MicrosoftObjectId = "oid-pretendente", DisplayName = "Pretendente" };
        var users = new FakeUsers(dono, pretendente);
        var caso = new LinkMinecraftAccount(new FakeExchange("mc-token"), new FakeProfiles("uuid-disputado"), users);

        var result = await caso.HandleAsync(pretendente.Id, "ms-token", Ct);

        result.Succeeded.ShouldBeFalse();
        pretendente.MinecraftUuid.ShouldBeNull();
    }

    [Fact]
    public async Task Recusa_quando_a_conta_microsoft_nao_tem_o_jogo()
    {
        var user = new User { MicrosoftObjectId = "oid", DisplayName = "Ana" };
        var users = new FakeUsers(user);
        var caso = new LinkMinecraftAccount(new FakeExchange("mc-token"), new FakeProfiles(null), users);

        var result = await caso.HandleAsync(user.Id, "ms-token", Ct);

        result.Succeeded.ShouldBeFalse();
        user.MinecraftUuid.ShouldBeNull();
    }

    private sealed class FakeExchange(string? token) : IMinecraftTokenExchange
    {
        public Task<Result<string>> ExchangeAsync(string microsoftAccessToken, CancellationToken ct) =>
            Task.FromResult(token is null
                ? Result<string>.Fail("sem Minecraft")
                : Result<string>.Success(token));
    }

    private sealed class FakeProfiles(string? uuid) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(uuid is null ? null : new MinecraftProfile(uuid, "nome"));
    }
}
