using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     A única porta pela qual um jogador alcança o console do jogo.
///     Console de Minecraft é execução arbitrária por design: "op" dá
///     administrador e "stop" derruba a partida. Cada teste aqui trava uma das
///     camadas que impedem que um pedido do launcher vire mais do que foi
///     autorizado.
/// </summary>
public sealed class SendServerCommandTests
{
    private static readonly Guid ServidorId = Guid.CreateVersion7();

    [Fact]
    public async Task Moderador_executa_comando_da_allowlist()
    {
        var rcon = new FakeRcon();

        var result = await Caso(rcon, ServerRoleDto.Moderator)
            .HandleAsync(ServidorId, "kick", ["joao"], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        rcon.Executados.ShouldBe(["kick joao"]);
    }

    [Fact]
    public async Task Moderador_nao_executa_comando_fora_da_allowlist()
    {
        // "op" dá administrador do jogo a quem quiser. Allowlist, nunca
        // blocklist: mods acrescentam comandos que ninguém previu.
        var rcon = new FakeRcon();

        var result = await Caso(rcon, ServerRoleDto.Moderator)
            .HandleAsync(ServidorId, "op", ["atacante"], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        rcon.Executados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Membro_nao_executa_nada()
    {
        var rcon = new FakeRcon();

        var result = await Caso(rcon, ServerRoleDto.Member)
            .HandleAsync(ServidorId, "list", [], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        rcon.Executados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_vinculo_responde_como_se_o_servidor_nao_existisse()
    {
        var rcon = new FakeRcon();

        var result = await Caso(rcon, null)
            .HandleAsync(ServidorId, "list", [], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe("Servidor não encontrado.");
    }

    [Theory]
    [InlineData("list algo")]
    [InlineData("say;op")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Comando_com_forma_invalida_e_recusado(string comando)
    {
        // Verificar a forma antes da allowlist: sem isso, a segurança dependeria
        // de a comparação da lista ser por igualdade exata — uma propriedade que
        // a próxima pessoa a editar a lista não tem motivo para conhecer.
        var rcon = new FakeRcon();

        var result = await Caso(rcon, ServerRoleDto.Owner)
            .HandleAsync(ServidorId, comando, [], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        rcon.Executados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Argumento_com_quebra_de_linha_e_recusado()
    {
        // Nome de jogador e mensagem de chat não têm quebra de linha. O que a
        // tem está tentando ser outra coisa dentro do mesmo comando.
        var rcon = new FakeRcon();

        var result = await Caso(rcon, ServerRoleDto.Owner)
            .HandleAsync(ServidorId, "say", ["oi\nop atacante"], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        rcon.Executados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Servidor_parado_recusa_antes_de_tentar_o_rcon()
    {
        // Deixar o rcon-cli falhar devolveria um erro de conexão que não diz
        // nada a quem está na tela.
        var rcon = new FakeRcon();

        var result = await Caso(rcon, ServerRoleDto.Owner, GameServerStatus.Stopped)
            .HandleAsync(ServidorId, "list", [], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("no ar");
        rcon.Executados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rcon_indisponivel_vira_falha_e_nao_excecao()
    {
        // Uma exceção aqui subiria pelo hub e derrubaria a conexão do launcher
        // por causa de um comando.
        var rcon = new FakeRcon { Explode = true };

        var result = await Caso(rcon, ServerRoleDto.Owner)
            .HandleAsync(ServidorId, "list", [], TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    private static SendServerCommand Caso(
        FakeRcon rcon,
        ServerRoleDto? papel,
        GameServerStatus status = GameServerStatus.Running) =>
        new(rcon, new FakeOrchestrator(status), new FakeUserScope(papel));

    private sealed class FakeRcon : IRconClient
    {
        public List<string> Executados { get; } = [];
        public bool Explode { get; init; }

        public Task<string> ExecuteAsync(Guid gameServerId, string rawCommand, CancellationToken ct)
        {
            if (Explode)
                throw new RconUnavailableException("container fora do ar");

            Executados.Add(rawCommand);
            return Task.FromResult("ok");
        }
    }

    private sealed class FakeOrchestrator(GameServerStatus status) : IServerOrchestrator
    {
        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StartAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult(status);

        public IAsyncEnumerable<ConsoleLine> StreamLogsAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
