using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     A whitelist é o que responde "só quem eu deixar entra".
///     O Minecraft não tem senha de entrada — a lista de servidores do cliente
///     guarda endereço e nome, e o protocolo não prevê credencial. A whitelist é
///     o mecanismo que o jogo oferece, e o TCMine sabe quem são os convidados
///     porque o login do jogador é por perfil Minecraft verificado.
/// </summary>
public sealed class WhitelistTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Liga_a_lista_e_acrescenta_cada_membro()
    {
        var rcon = new FakeRcon();
        var servidor = Servidor(ligada: true, GameServerStatus.Running);

        await Sync(servidor, rcon, Membro("Steve", "uuid-1"), Membro("Alex", "uuid-2"))
            .HandleAsync(servidor.Id, Ct);

        rcon.Comandos.ShouldContain("whitelist on");
        rcon.Comandos.ShouldContain("whitelist add Steve");
        rcon.Comandos.ShouldContain("whitelist add Alex");

        // Sem o reload, o que foi escrito não passa a valer até o restart.
        rcon.Comandos.ShouldContain("whitelist reload");
    }

    [Fact]
    public async Task Desligada_apenas_desliga()
    {
        // Não precisa da lista: quem estava dentro continua, e a porta abre.
        var rcon = new FakeRcon();
        var servidor = Servidor(ligada: false, GameServerStatus.Running);

        await Sync(servidor, rcon, Membro("Steve", "uuid-1")).HandleAsync(servidor.Id, Ct);

        rcon.Comandos.ShouldBe(["whitelist off"]);
    }

    [Fact]
    public async Task Membro_sem_perfil_minecraft_e_ignorado()
    {
        // Conta criada no painel que ainda não entrou no jogo: não há nome de
        // jogador para adicionar. Entra sozinha no primeiro login.
        var rcon = new FakeRcon();
        var servidor = Servidor(ligada: true, GameServerStatus.Running);

        await Sync(servidor, rcon, Membro("Steve", null)).HandleAsync(servidor.Id, Ct);

        rcon.Comandos.ShouldNotContain("whitelist add Steve");
    }

    [Fact]
    public async Task Servidor_parado_nao_recebe_comando()
    {
        // Sem RCON não há o que fazer, e falhar aqui abortaria o resgate de um
        // convite perfeitamente válido. A próxima subida sincroniza.
        var rcon = new FakeRcon();
        var servidor = Servidor(ligada: true, GameServerStatus.Stopped);

        await Sync(servidor, rcon, Membro("Steve", "uuid-1")).HandleAsync(servidor.Id, Ct);

        rcon.Comandos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Falha_de_rcon_nao_propaga()
    {
        // Resgatar um convite vale mesmo que a whitelist não tenha entrado: o
        // vínculo está gravado e a próxima subida refaz a lista.
        var servidor = Servidor(ligada: true, GameServerStatus.Running);
        var rcon = new FakeRcon { Estoura = true };

        await Should.NotThrowAsync(() =>
            Sync(servidor, rcon, Membro("Steve", "uuid-1")).HandleAsync(servidor.Id, Ct));
    }

    private static SyncServerWhitelist Sync(
        GameServer servidor, FakeRcon rcon, params ServerMemberView[] membros) =>
        new(new FakeServers(servidor), new MembrosFixos(membros), rcon,
            NullLogger<SyncServerWhitelist>.Instance);

    private static ServerMemberView Membro(string nome, string? uuid) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), nome, uuid, ServerRoleDto.Member, null);

    private static GameServer Servidor(bool ligada, GameServerStatus status) => new()
    {
        Name = "Servidor",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogar.exemplo.com",
        RconSecret = "segredo",
        WhitelistEnabled = ligada,
        Status = status
    };

    private sealed class FakeRcon : IRconClient
    {
        public List<string> Comandos { get; } = [];
        public bool Estoura { get; init; }

        public Task<string> ExecuteAsync(Guid gameServerId, string rawCommand, CancellationToken ct)
        {
            if (Estoura)
                throw new InvalidOperationException("rcon fora do ar");

            Comandos.Add(rawCommand);
            return Task.FromResult("");
        }
    }

    private sealed class FakeServers(GameServer servidor) : FakeServerRepositoryBase
    {
        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<GameServer?>(servidor);
    }

    /// <summary>
    ///     Devolve as visões que o teste montou — o fake compartilhado sempre
    ///     devolve UUID nulo, e é justamente o UUID que decide se o membro entra
    ///     na lista.
    /// </summary>
    private sealed class MembrosFixos(ServerMemberView[] membros) : IMembershipRepository
    {
        public Task<IReadOnlyList<ServerMemberView>> ListWithUsersAsync(
            Guid gameServerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ServerMemberView>>(membros);

        public Task AddAsync(Membership membership, CancellationToken ct) => throw new NotSupportedException();

        public Task<Membership?> GetAsync(Guid userId, Guid gameServerId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Membership>> ListByServerAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Membership>> ListByUserAsync(Guid userId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Membership membership, CancellationToken ct) => throw new NotSupportedException();

        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
