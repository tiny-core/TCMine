using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Login do painel pela Microsoft — e, ao mesmo tempo, o setup inicial.
///     O caso que mais importa aqui é a adoção: uma conta criada antes pelo
///     launcher (só com Minecraft, sem oid) tem de virar a MESMA conta quando a
///     mesma pessoa entra no painel pela primeira vez — é a fenda que fazia o
///     launcher do admin não enxergar o próprio servidor dele.
/// </summary>
public sealed class AuthenticateMicrosoftUserTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Primeiro_usuario_da_instalacao_vira_admin()
    {
        var users = new FakeUsers();
        var caso = Build(users);

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Succeeded.ShouldBeTrue();
        result.Value!.IsInstanceAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task Segundo_usuario_nao_vira_admin()
    {
        var jaExiste = new User { MicrosoftObjectId = "oid-outro", DisplayName = "Outro", IsInstanceAdmin = true };
        var users = new FakeUsers(jaExiste);
        var caso = Build(users);

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Succeeded.ShouldBeTrue();
        result.Value!.IsInstanceAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task Quem_ja_tem_oid_e_reconhecido_e_atualizado_sem_duplicar()
    {
        var existente = new User
        {
            MicrosoftObjectId = "oid-fixo", DisplayName = "nome-antigo", IsInstanceAdmin = true
        };
        var users = new FakeUsers(existente);
        var caso = Build(users, oid: "oid-fixo", nome: "nome-novo");

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Value.ShouldBeSameAs(existente);
        result.Value!.DisplayName.ShouldBe("nome-novo");
        users.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Conta_do_launcher_e_adotada_em_vez_de_duplicada()
    {
        // A MESMA pessoa já tinha entrado pelo launcher (AuthenticateMinecraftUser),
        // o que grava MinecraftUuid e deixa MicrosoftObjectId nulo. Login pela
        // Microsoft com essa mesma conta tem de reconhecer o Minecraft e
        // completar o oid na linha que já existe — não criar uma segunda.
        var doLauncher = new User { DisplayName = "jogador", MinecraftUuid = "uuid-123" };
        var users = new FakeUsers(doLauncher);
        var caso = Build(users, oid: "oid-novo", minecraftUuid: "uuid-123");

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Value.ShouldBeSameAs(doLauncher);
        result.Value!.MicrosoftObjectId.ShouldBe("oid-novo");
        users.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Conta_do_painel_sem_minecraft_absorve_a_duplicata_do_launcher()
    {
        // Ordem inversa da adoção: o painel veio primeiro, sem Minecraft, e o
        // launcher criou depois uma segunda conta só com o UUID. No próximo
        // login do painel o Minecraft é resolvido e as duas viram uma.
        var painel = new User { MicrosoftObjectId = "oid-ana", DisplayName = "Ana" };
        var doLauncher = new User { DisplayName = "ana", MinecraftUuid = "uuid-ana" };
        var users = new FakeUsers(painel, doLauncher);
        var caso = Build(users, oid: "oid-ana", minecraftUuid: "uuid-ana");

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Value.ShouldBeSameAs(painel);
        painel.MinecraftUuid.ShouldBe("uuid-ana");
        users.Fusoes.ShouldBe([(painel.Id, doLauncher.Id)]);
        users.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Minecraft_de_outra_conta_microsoft_nao_e_fundido()
    {
        var painel = new User { MicrosoftObjectId = "oid-ana", DisplayName = "Ana" };
        var outra = new User { MicrosoftObjectId = "oid-bia", DisplayName = "Bia", MinecraftUuid = "uuid-x" };
        var users = new FakeUsers(painel, outra);
        var caso = Build(users, oid: "oid-ana", minecraftUuid: "uuid-x");

        await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        users.Fusoes.ShouldBeEmpty();
        painel.MinecraftUuid.ShouldBeNull();
    }

    [Fact]
    public async Task Sem_minecraft_a_conta_nasce_mesmo_assim()
    {
        // O jogo é oportunista para quem entra pelo painel — diferente do
        // launcher, onde a posse é obrigatória.
        var users = new FakeUsers();
        var caso = Build(users, minecraftToken: null);

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Succeeded.ShouldBeTrue();
        result.Value!.MinecraftUuid.ShouldBeNull();
    }

    [Fact]
    public async Task Microsoft_recusando_o_codigo_e_falha()
    {
        var caso = Build(new FakeUsers(), oauthFalha: "código inválido");

        var result = await caso.HandleAsync("client", "code", "https://x/callback", "verifier", Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe("código inválido");
    }

    private static AuthenticateMicrosoftUser Build(
        FakeUsers users,
        string oid = "oid-1",
        string nome = "Jogador",
        string? minecraftToken = "mc-token",
        string? minecraftUuid = null,
        string? oauthFalha = null) =>
        new(
            new FakeOAuth(oid, nome, oauthFalha),
            new FakeExchange(minecraftToken),
            new FakeProfiles(minecraftUuid is null ? null : new MinecraftProfile(minecraftUuid, nome)),
            users,
            new FakeActivityLog(),
            NullLogger<AuthenticateMicrosoftUser>.Instance);

    private sealed class FakeOAuth(string oid, string nome, string? falha) : IMicrosoftOAuthClient
    {
        public Task<Result<MicrosoftIdentity>> ExchangeCodeAsync(
            string clientId, string code, string redirectUri, string codeVerifier, CancellationToken ct) =>
            Task.FromResult(falha is not null
                ? Result<MicrosoftIdentity>.Fail(falha)
                : Result<MicrosoftIdentity>.Success(new MicrosoftIdentity(oid, nome, "ms-token")));
    }

    private sealed class FakeExchange(string? token) : IMinecraftTokenExchange
    {
        public Task<Result<string>> ExchangeAsync(string microsoftAccessToken, CancellationToken ct) =>
            Task.FromResult(token is null
                ? Result<string>.Fail("sem Minecraft")
                : Result<string>.Success(token));
    }

    private sealed class FakeProfiles(MinecraftProfile? profile) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(profile);
    }
}
