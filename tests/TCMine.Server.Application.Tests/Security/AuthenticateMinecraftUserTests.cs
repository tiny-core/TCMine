using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Login do jogador pelo launcher.
///     O que cada teste trava: que ninguém entra sem a Mojang confirmar, que
///     quem volta é reconhecido pelo UUID (e não pelo nome, que muda), e que a
///     conta criada não ganha nenhum caminho de login local de brinde.
/// </summary>
public sealed class AuthenticateMinecraftUserTests
{
    [Fact]
    public async Task Cria_usuario_no_primeiro_login()
    {
        var users = new FakeUsers();
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("ana", "abc123"));

        var resultado = await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeTrue();
        users.Adicionado.ShouldNotBeNull();
        users.Adicionado.MinecraftUuid.ShouldBe("abc123");
        users.Adicionado.DisplayName.ShouldBe("ana");
    }

    [Fact]
    public async Task Conta_criada_pelo_launcher_nao_tem_senha_nem_email()
    {
        var users = new FakeUsers();
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("ana", "abc123"));

        await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        // Sem estas duas garantias a conta apareceria como alvo de login local
        // e de recuperação de senha — dois caminhos que ela não deveria ter.
        users.Adicionado!.PasswordHash.ShouldBeNull();
        users.Adicionado.Email.ShouldBeNull();
    }

    [Fact]
    public async Task Reconhece_quem_volta_pelo_uuid_e_nao_pelo_nome()
    {
        var existente = new User
        {
            DisplayName = "nome-antigo",
            MinecraftUuid = "abc123"
        };
        var users = new FakeUsers(existente);
        var caso = new AuthenticateMinecraftUser(users, new FakeProfiles("nome-novo", "abc123"));

        var resultado = await caso.HandleAsync("token-bom", TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeTrue();
        resultado.Value.ShouldBeSameAs(existente);

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

        var resultado = await caso.HandleAsync("token-ruim", TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeFalse();
        users.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Recusa_token_vazio_sem_ir_a_mojang()
    {
        var users = new FakeUsers();
        var profiles = new FakeProfiles("ana", "abc123");
        var caso = new AuthenticateMinecraftUser(users, profiles);

        var resultado = await caso.HandleAsync("   ", TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeFalse();
        profiles.Consultado.ShouldBeFalse();
    }

    private sealed class FakeProfiles : IMinecraftProfileSource
    {
        private readonly MinecraftProfile? _profile;

        public FakeProfiles(string name, string uuid) => _profile = new MinecraftProfile(uuid, name);

        public FakeProfiles(MinecraftProfile? profile) => _profile = profile;

        public bool Consultado { get; private set; }

        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct)
        {
            Consultado = true;
            return Task.FromResult(_profile);
        }
    }
}
