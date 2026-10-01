using TCMine.Contracts.Servers;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     O que o launcher lista.
///     A garantia negativa é a que importa mais: um servidor COM whitelist em
///     que o jogador não tem vínculo não pode aparecer — nome e endereço de
///     conexão são justamente o que alguém precisaria para tentar entrar onde
///     não foi chamado. Um servidor SEM whitelist é o oposto: é público por
///     definição, então esconder a existência dele do próprio jogador não
///     protege nada, só impede de achar algo que estava aberto.
/// </summary>
public sealed class ListAccessibleServersTests
{
    [Fact]
    public async Task Sem_vinculo_a_lista_vem_vazia()
    {
        var meu = Servidor("Meu");
        var alheio = Servidor("Alheio");

        var lista = await new ListAccessibleServers(
                new FakeServers(meu, alheio), new FakeMemberships(), Jogador(Guid.CreateVersion7()))
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.ShouldBeEmpty();
    }

    [Fact]
    public async Task So_aparecem_os_servidores_em_que_ha_vinculo()
    {
        var player = Guid.CreateVersion7();
        var meu = Servidor("Meu");
        var alheio = Servidor("Alheio");

        var lista = await new ListAccessibleServers(
                new FakeServers(meu, alheio),
                new FakeMemberships(new Membership
                {
                    UserId = player,
                    GameServerId = meu.Id,
                    Role = ServerRole.Moderator
                }),
                Jogador(player))
            .HandleAsync(TestContext.Current.CancellationToken);

        var unico = lista.ShouldHaveSingleItem();
        unico.Server.Name.ShouldBe("Meu");

        // O papel vem junto: sem ele a interface teria de perguntar de novo,
        // servidor por servidor.
        unico.Role.ShouldBe(ServerRoleDto.Moderator);
    }

    [Fact]
    public async Task Admin_da_instalacao_ve_tudo_como_dono()
    {
        // Mesma regra que o ICurrentUserScope aplica ao responder o papel. Sem
        // ela o painel do admin apareceria vazio por não haver Membership dos
        // servidores criados antes do modelo de convites existir.
        var lista = await new ListAccessibleServers(
                new FakeServers(Servidor("A"), Servidor("B")),
                new FakeMemberships(),
                new FakeUserScope { IsInstanceAdmin = true })
            .HandleAsync(TestContext.Current.CancellationToken);

        lista.Count.ShouldBe(2);
        lista.ShouldAllBe(s => s.Role == ServerRoleDto.Owner);
    }

    [Fact]
    public async Task Servidor_publico_aparece_mesmo_sem_vinculo()
    {
        var player = Guid.CreateVersion7();
        var publico = Servidor("Público", whitelistEnabled: false);
        var privado = Servidor("Privado");

        var lista = await new ListAccessibleServers(
                new FakeServers(publico, privado), new FakeMemberships(), Jogador(player))
            .HandleAsync(TestContext.Current.CancellationToken);

        // O privado continua escondido de quem não foi convidado; só a
        // ausência de whitelist abre a visibilidade, não a falta de vínculo em
        // si.
        var unico = lista.ShouldHaveSingleItem();
        unico.Server.Name.ShouldBe("Público");

        // Member: o mesmo "vê status, sem console" de quem foi convidado só
        // para jogar — ver um servidor público não deve dar mais controle do
        // que um convite comum daria.
        unico.Role.ShouldBe(ServerRoleDto.Member);
    }

    [Fact]
    public async Task Sem_sessao_a_lista_vem_vazia()
    {
        var lista = await new ListAccessibleServers(
                new FakeServers(Servidor("A")),
                new FakeMemberships(),
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
}
