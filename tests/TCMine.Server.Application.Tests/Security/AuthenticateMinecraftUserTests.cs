using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Login do jogador pelo launcher.
///     O que cada teste trava: que ninguém entra sem a Mojang confirmar, que
///     quem volta é reconhecido pelo UUID (e não pelo nome, que muda), e que a
///     conta criada pelo launcher não ganha identidade Microsoft de brinde.
/// </summary>
public sealed class AuthenticateMinecraftUserTests
{
    [Fact]
    public async Task Cria_usuario_no_primeiro_login()
    {
        var users = new FakeUsers();
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("ana", "abc123"));

        var result = await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        users.Adicionado.ShouldNotBeNull();
        users.Adicionado.MinecraftUuid.ShouldBe("abc123");
        users.Adicionado.DisplayName.ShouldBe("ana");
    }

    [Fact]
    public async Task Dois_logins_simultaneos_do_mesmo_jogador_nao_criam_duas_contas()
    {
        // O launcher dispara o login silencioso e o da tela juntos no primeiro
        // arranque: os dois veem "ninguém ainda". O índice único segura a
        // segunda linha, e o caso de uso adota a que venceu em vez de falhar.
        var vencedor = new User { DisplayName = "ana", MinecraftUuid = "abc123" };
        var users = new FakeUsers { Concorrente = vencedor };
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("ana", "abc123"));

        var result = await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBeSameAs(vencedor);
        users.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Conta_criada_pelo_launcher_nao_tem_identidade_microsoft()
    {
        var users = new FakeUsers();
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("ana", "abc123"));

        await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        // O launcher manda só o token do Minecraft, nunca o da Microsoft — uma
        // conta criada por ele não tem como ter ganho um oid de brinde.
        users.Adicionado!.MicrosoftObjectId.ShouldBeNull();
    }

    [Fact]
    public async Task Reconhece_quem_volta_pelo_uuid_e_nao_pelo_nome()
    {
        var existente = new User { DisplayName = "nome-antigo", MinecraftUuid = "abc123" };
        var users = new FakeUsers(existente);
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("nome-novo", "abc123"));

        var result = await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBeSameAs(existente);

        // Uma conta duplicada aqui significaria o jogador perdendo os próprios
        // vínculos toda vez que trocasse de nome no jogo.
        users.Adicionado.ShouldBeNull();
        existente.DisplayName.ShouldBe("nome-novo");
    }

    [Fact]
    public async Task Recusa_quando_a_mojang_nao_reconhece_o_token()
    {
        var users = new FakeUsers();
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles(null));

        var result = await caso.HandleAsync("token-ruim", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        users.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Recusa_token_vazio_sem_ir_a_mojang()
    {
        var users = new FakeUsers();
        var profiles = new FakeProfiles("ana", "abc123");
        var caso = new AuthenticateMinecraftUser(users, profiles);

        var result = await caso.HandleAsync("   ", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        profiles.Consultado.ShouldBeFalse();
    }

    private sealed class FakeProfiles : IMinecraftProfileSource
    {
        private readonly MinecraftProfile? _profile;

        public FakeProfiles(string name, string uuid)
        {
            _profile = new MinecraftProfile(uuid, name);
        }

        public FakeProfiles(MinecraftProfile? profile)
        {
            _profile = profile;
        }

        public bool Consultado { get; private set; }

        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct)
        {
            Consultado = true;
            return Task.FromResult(_profile);
        }
    }
}
