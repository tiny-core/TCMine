using TCMine.Contracts.Servers;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     O que o launcher lista.
///     TODO servidor aparece — mesmo o que o jogador ainda não pode entrar: é o
///     que torna "Pedir acesso" possível. A garantia que importa mudou de
///     "esconder o servidor" para "esconder só o endereço" (ver
///     <c>ServerMappings.ToDto</c>, no servidor) — este caso de uso só decide
///     QUAL estado cada servidor tem para o jogador atual.
/// </summary>
public sealed class ListAccessibleServersTests
{
    [Fact]
    public async Task Sem_vinculo_servidor_publico_vem_granted_e_privado_vem_none()
    {
        var player = Guid.CreateVersion7();
        var publico = Servidor("Público", whitelistEnabled: false);
        var privado = Servidor("Privado");

        var lista = await new ListAccessibleServers(
                new FakeServers(publico, privado), new FakeMemberships(), new FakeAccessRequests(),
                Jogador(player))
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.Count.ShouldBe(2);

        var doPublico = lista.Single(s => s.Server.Name == "Público");
        doPublico.AccessState.ShouldBe(ServerAccessState.Granted);

        var doPrivado = lista.Single(s => s.Server.Name == "Privado");
        doPrivado.AccessState.ShouldBe(ServerAccessState.None);
    }

    [Fact]
    public async Task Com_vinculo_o_servidor_vem_granted_com_o_papel()
    {
        var player = Guid.CreateVersion7();
        var meu = Servidor("Meu");

        var lista = await new ListAccessibleServers(
                new FakeServers(meu),
                new FakeMemberships(new Membership
                {
                    UserId = player,
                    GameServerId = meu.Id,
                    Role = ServerRole.Moderator
                }),
                new FakeAccessRequests(),
                Jogador(player))
            .HandleAsync(TestContext.Current.CancellationToken);

        var unico = lista.ShouldHaveSingleItem();
        unico.AccessState.ShouldBe(ServerAccessState.Granted);

        // O papel vem junto: sem ele a interface teria de perguntar de novo,
        // servidor por servidor.
        unico.Role.ShouldBe(ServerRoleDto.Moderator);
    }

    [Fact]
    public async Task Pedido_pendente_marca_o_servidor_como_pending()
    {
        var player = Guid.CreateVersion7();
        var privado = Servidor("Privado");

        var lista = await new ListAccessibleServers(
                new FakeServers(privado),
                new FakeMemberships(),
                new FakeAccessRequests(privado.Id),
                Jogador(player))
            .HandleAsync(TestContext.Current.CancellationToken);

        var unico = lista.ShouldHaveSingleItem();
        unico.AccessState.ShouldBe(ServerAccessState.Pending);
    }

    [Fact]
    public async Task Admin_da_instalacao_ve_tudo_como_dono_e_liberado()
    {
        // Mesma regra que o ICurrentUserScope aplica ao responder o papel. Sem
        // ela o painel do admin apareceria vazio por não haver Membership dos
        // servidores criados antes do modelo de convites existir.
        var lista = await new ListAccessibleServers(
                new FakeServers(Servidor("A"), Servidor("B")),
                new FakeMemberships(),
                new FakeAccessRequests(),
                new FakeUserScope { IsInstanceAdmin = true })
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.Count.ShouldBe(2);
        lista.ShouldAllBe(s => s.Role == ServerRoleDto.Owner && s.AccessState == ServerAccessState.Granted);
    }

    [Fact]
    public async Task Sem_sessao_a_lista_vem_vazia()
    {
        var lista = await new ListAccessibleServers(
                new FakeServers(Servidor("A")),
                new FakeMemberships(),
                new FakeAccessRequests(),
                new FakeUserScope { UserId = null })
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.ShouldBeEmpty();
    }

    private static FakeUserScope Jogador(Guid id) => new(null) { UserId = id };

    private static GameServer Servidor(string name, bool whitelistEnabled = true) => new()
    {
        Name = name,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = $"{name.ToLowerInvariant()}:25565",
        RconSecret = "segredo",
        WhitelistEnabled = whitelistEnabled
    };

    private sealed class FakeServers(params GameServer[] seed) : FakeServerRepositoryBase
    {
        public override Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>(seed);
    }

    /// <summary>Pedidos pendentes do ÚNICO jogador de cada teste, por servidor.</summary>
    private sealed class FakeAccessRequests(params Guid[] gameServerIdsPendentes) : FakeAccessRequestRepositoryBase
    {
        public override Task<IReadOnlyList<AccessRequest>> ListPendingByUserAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccessRequest>>(
                [.. gameServerIdsPendentes.Select(id => new AccessRequest { UserId = userId, GameServerId = id })]);
    }
}
