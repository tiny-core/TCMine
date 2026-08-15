using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

public sealed class ServerLifecycleTests
{
    [Fact]
    public async Task Parar_manda_parar_e_nao_iniciar()
    {
        // Regressão de um copiar-e-colar: o StopGameServer chamava StartAsync no
        // orquestrador (comentários inclusive). Clicar em "parar" ligava o
        // servidor — e, num servidor já parado, o admin via o container subir.
        var server = NovoServidor();
        var orchestrator = new FakeOrchestrator();

        var result = await new StopGameServer(orchestrator, new FakeServerRepo(server), new FakeJobProgress())
            .HandleAsync(server.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["Stop", "GetStatus"], orchestrator.Chamadas);
    }

    [Fact]
    public async Task Parar_espera_o_mundo_salvar_antes_de_matar_o_container()
    {
        // O stop-server.sh do itzg salva o mundo ao receber SIGTERM. Um timeout
        // curto mataria o processo no meio da gravação e corromperia chunks.
        var server = NovoServidor();
        var orchestrator = new FakeOrchestrator();

        await new StopGameServer(orchestrator, new FakeServerRepo(server), new FakeJobProgress())
            .HandleAsync(server.Id, CancellationToken.None);

        Assert.True(orchestrator.StopTimeout >= TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Iniciar_manda_iniciar_e_grava_o_status_real()
    {
        var server = NovoServidor();
        var repo = new FakeServerRepo(server);
        var orchestrator = new FakeOrchestrator { Status = GameServerStatus.Running };

        var result = await new StartGameServer(orchestrator, repo, new FakeJobProgress())
            .HandleAsync(server.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Start", orchestrator.Chamadas);
        Assert.Equal(GameServerStatus.Running, repo.Saved?.Status);
    }

    // ---- Fixtures ----

    private static GameServer NovoServidor() => new()
    {
        Name = "Servidor de Teste",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogo.exemplo:25565",
        RconSecret = "segredo"
    };

    // ---- Fakes ----

    private sealed class FakeOrchestrator : IServerOrchestrator
    {
        public List<string> Chamadas { get; } = [];
        public GameServerStatus Status { get; init; } = GameServerStatus.Stopped;
        public TimeSpan StopTimeout { get; private set; }

        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct)
        {
            Chamadas.Add("EnsureCreated");
            return Task.FromResult("container-1");
        }

        public Task StartAsync(Guid gameServerId, CancellationToken ct)
        {
            Chamadas.Add("Start");
            return Task.CompletedTask;
        }

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct)
        {
            Chamadas.Add("Stop");
            StopTimeout = timeout;
            return Task.CompletedTask;
        }

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct)
        {
            Chamadas.Add("GetStatus");
            return Task.FromResult(Status);
        }

        public IAsyncEnumerable<string> StreamLogsAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeServerRepo(GameServer server) : FakeServerRepositoryBase
    {
        public GameServer? Saved { get; private set; }

        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<GameServer?>(server);

        public override Task UpdateAsync(GameServer s, CancellationToken ct)
        {
            Saved = s;
            return Task.CompletedTask;
        }

    }
}
