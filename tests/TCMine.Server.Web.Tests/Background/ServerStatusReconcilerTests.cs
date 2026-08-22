using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Tests.Background;

/// <summary>
///     A reconciliação de status no arranque.
///     Containers sobem com <c>unless-stopped</c> e sobrevivem ao reinício do
///     painel, enquanto a coluna guarda o que valia antes de ele cair. Abrir a
///     página conserta — mas o coletor de métricas pula servidor que não está
///     marcado como Running, e ele não espera ninguém abrir página nenhuma.
/// </summary>
public sealed class ServerStatusReconcilerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Servidor_que_continuou_no_ar_volta_a_constar_como_Running()
    {
        // O caso que motivou tudo: o painel reinicia, o container nunca parou.
        var servidor = Servidor(GameServerStatus.Stopped);
        var repo = new FakeServers(servidor);

        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            repo, new FakeOrchestrator(GameServerStatus.Running), Ct);

        corrigidos.ShouldBe(1);
        servidor.Status.ShouldBe(GameServerStatus.Running);
        repo.Gravados.ShouldBe([servidor.Id]);
    }

    [Fact]
    public async Task Servidor_que_caiu_enquanto_o_painel_estava_fora_vira_Crashed()
    {
        var servidor = Servidor(GameServerStatus.Running);

        await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(servidor), new FakeOrchestrator(GameServerStatus.Crashed), Ct);

        servidor.Status.ShouldBe(GameServerStatus.Crashed);
    }

    [Fact]
    public async Task Status_que_ja_estava_certo_nao_gera_escrita()
    {
        // Sem esta guarda, todo arranque reescreveria a tabela inteira sem ter
        // mudado nada.
        var repo = new FakeServers(Servidor(GameServerStatus.Running));

        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            repo, new FakeOrchestrator(GameServerStatus.Running), Ct);

        corrigidos.ShouldBe(0);
        repo.Gravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Container_que_nao_responde_nao_interrompe_os_outros()
    {
        // Um container removido por fora, ou um daemon lento: o resto da
        // varredura precisa continuar, senão um servidor problemático deixaria
        // todos os demais desatualizados.
        var quebrado = Servidor(GameServerStatus.Running);
        var bom = Servidor(GameServerStatus.Stopped);

        var orchestrator = new FakeOrchestrator(GameServerStatus.Running)
        {
            FalhaEm = quebrado.Id
        };

        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(quebrado, bom), orchestrator, Ct);

        corrigidos.ShouldBe(1);
        bom.Status.ShouldBe(GameServerStatus.Running);
        quebrado.Status.ShouldBe(GameServerStatus.Running, "não foi tocado");
    }

    [Fact]
    public async Task Sem_servidores_nao_faz_nada()
    {
        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(), new FakeOrchestrator(GameServerStatus.Running), Ct);

        corrigidos.ShouldBe(0);
    }

    private static GameServer Servidor(GameServerStatus status) => new()
    {
        Name = "Survival",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogo:25565",
        RconSecret = "segredo",
        Status = status
    };

    private sealed class FakeServers(params GameServer[] seed) : IServerRepository
    {
        public List<Guid> Gravados { get; } = [];

        public Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>(seed);

        public Task UpdateAsync(GameServer server, CancellationToken ct)
        {
            Gravados.Add(server.Id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task AddAsync(GameServer server, CancellationToken ct) => throw new NotImplementedException();
        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task AddBackupAsync(WorldBackup backup, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveBackupAsync(Guid backupId, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeOrchestrator(GameServerStatus real) : IServerOrchestrator
    {
        public Guid? FalhaEm { get; init; }

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            gameServerId == FalhaEm
                ? throw new InvalidOperationException("container sumiu")
                : Task.FromResult(real);

        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StartAsync(Guid gameServerId, CancellationToken ct) => throw new NotImplementedException();

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<ConsoleLine> StreamLogsAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) => throw new NotImplementedException();
    }
}
