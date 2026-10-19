using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Common;
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
        var server = Servidor(GameServerStatus.Stopped);
        var repo = new FakeServers(server);

        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            repo, new FakeOrchestrator(GameServerStatus.Running), null, Ct);

        corrigidos.ShouldBe(1);
        server.Status.ShouldBe(GameServerStatus.Running);
        repo.Gravados.ShouldBe([server.Id]);
    }

    [Fact]
    public async Task Servidor_que_caiu_enquanto_o_painel_estava_fora_vira_Crashed()
    {
        var server = Servidor(GameServerStatus.Running);

        await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(server), new FakeOrchestrator(GameServerStatus.Crashed), null, Ct);

        server.Status.ShouldBe(GameServerStatus.Crashed);
    }

    [Fact]
    public void Espera_entre_tentativas_cresce_e_tem_teto()
    {
        // Docker fora do ar numa máquina de desenvolvimento não pode virar um
        // laço apertado, nem uma espera de horas quando ele volta.
        ServerStatusReconciler.RetryDelay(1).ShouldBe(TimeSpan.FromSeconds(5));
        ServerStatusReconciler.RetryDelay(3).ShouldBe(TimeSpan.FromSeconds(15));
        ServerStatusReconciler.RetryDelay(1000).ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Status_que_ja_estava_certo_nao_gera_escrita()
    {
        // Sem esta guarda, todo arranque reescreveria a tabela inteira sem ter
        // mudado nada. Com mundo já marcado: senão a marca seria a escrita.
        var repo = new FakeServers(Servidor(GameServerStatus.Running, comMundo: true));

        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            repo, new FakeOrchestrator(GameServerStatus.Running), null, Ct);

        corrigidos.ShouldBe(0);
        repo.Gravados.ShouldBeEmpty();
        repo.MundosMarcados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Container_que_nao_responde_nao_interrompe_os_outros()
    {
        // Um container removido por fora, ou um daemon lento: o resto da
        // varredura precisa continuar, senão um servidor problemático deixaria
        // todos os demais desatualizados.
        var quebrado = Servidor(GameServerStatus.Running);
        var bom = Servidor(GameServerStatus.Stopped);

        var orchestrator = new FakeOrchestrator(GameServerStatus.Running) { FalhaEm = quebrado.Id };

        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(quebrado, bom), orchestrator, null, Ct);

        corrigidos.ShouldBe(1);
        bom.Status.ShouldBe(GameServerStatus.Running);
        quebrado.Status.ShouldBe(GameServerStatus.Running, "não foi tocado");
    }

    [Fact]
    public async Task Sem_servidores_nao_faz_nada()
    {
        var corrigidos = await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(), new FakeOrchestrator(GameServerStatus.Running), null, Ct);

        corrigidos.ShouldBe(0);
    }

    private static GameServer Servidor(GameServerStatus status, bool comMundo = false) => new()
    {
        Name = "Survival",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogo:25565",
        RconSecret = "segredo",
        Status = status,
        WorldInitializedAt = comMundo ? DateTimeOffset.UtcNow.AddDays(-1) : null
    };

    [Fact]
    public async Task Entrar_em_Running_marca_que_o_servidor_tem_mundo()
    {
        // Regressão: nada gravava a marca, HasWorld era sempre falso e a troca
        // de versão rodava sem backup. Running = passou na verificação de
        // saúde = o jogo já gerou o mundo.
        var server = Servidor(GameServerStatus.Starting);
        var repo = new FakeServers(server);

        await ServerStatusTransitions.ApplyAsync(server, GameServerStatus.Running, repo, null, null, Ct);

        repo.MundosMarcados.ShouldBe([server.Id]);
        server.HasWorld.ShouldBeTrue("a cópia de quem chamou passa a concordar com o banco");
    }

    [Fact]
    public async Task Servidor_que_ja_estava_no_ar_sem_marca_tambem_a_ganha()
    {
        // O servidor de produção: está Running desde antes desta regra e nunca
        // mais passa por uma transição. A marca não pode depender de "mudou".
        var server = Servidor(GameServerStatus.Running);
        var repo = new FakeServers(server);

        var mudou = await ServerStatusTransitions.ApplyAsync(
            server, GameServerStatus.Running, repo, null, null, Ct);

        mudou.ShouldBeFalse("o status não mudou");
        repo.MundosMarcados.ShouldBe([server.Id]);
    }

    [Fact]
    public async Task Servidor_que_ja_tem_a_marca_nao_grava_de_novo()
    {
        var server = Servidor(GameServerStatus.Starting, comMundo: true);
        var repo = new FakeServers(server);

        await ServerStatusTransitions.ApplyAsync(server, GameServerStatus.Running, repo, null, null, Ct);

        repo.MundosMarcados.ShouldBeEmpty();
    }

    [Fact]
    public async Task So_Running_marca_o_mundo()
    {
        // Iniciando ainda pode cair antes de gerar o mundo; parado e caído não
        // provam nada.
        var server = Servidor(GameServerStatus.Stopped);
        var repo = new FakeServers(server);

        await ServerStatusTransitions.ApplyAsync(server, GameServerStatus.Starting, repo, null, null, Ct);
        await ServerStatusTransitions.ApplyAsync(server, GameServerStatus.Crashed, repo, null, null, Ct);

        repo.MundosMarcados.ShouldBeEmpty();
        server.HasWorld.ShouldBeFalse();
    }

    [Fact]
    public async Task Queda_vista_pelos_eventos_fica_no_registro_de_atividade()
    {
        var server = Servidor(GameServerStatus.Running);
        var activity = new FakeActivity();

        var mudou = await ServerStatusTransitions.ApplyAsync(
            server, GameServerStatus.Crashed, new FakeServers(server), activity, null, Ct);

        mudou.ShouldBeTrue();
        activity.Registrados.ShouldHaveSingleItem().Kind.ShouldBe(ActivityEventKind.ServerCrashed);
    }

    [Fact]
    public async Task Servidor_que_continua_caido_nao_repete_o_registro()
    {
        var server = Servidor(GameServerStatus.Crashed);
        var activity = new FakeActivity();
        var repo = new FakeServers(server);

        var mudou = await ServerStatusTransitions.ApplyAsync(
            server, GameServerStatus.Crashed, repo, activity, null, Ct);

        mudou.ShouldBeFalse();
        activity.Registrados.ShouldBeEmpty();
        repo.Gravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Entrar_em_Running_aplica_a_whitelist()
    {
        // Running = passou na verificação de saúde = o RCON responde. Antes a
        // sincronização só rodava logo após subir o container, ainda Iniciando,
        // e saía calada: a whitelist nunca entrava.
        var server = Servidor(GameServerStatus.Starting);
        var whitelist = new FakeWhitelist();

        await ServerStatusTransitions.ApplyAsync(
            server, GameServerStatus.Running, new FakeServers(server), null, whitelist, Ct);

        whitelist.Sincronizados.ShouldBe([server.Id]);
    }

    [Fact]
    public async Task Continuar_em_Running_nao_sincroniza_de_novo()
    {
        // Toda tela de servidores reconsulta o status: sincronizar a cada
        // abertura seria uma rajada de RCON por visita.
        var server = Servidor(GameServerStatus.Running);
        var whitelist = new FakeWhitelist();

        await ServerStatusTransitions.ApplyAsync(
            server, GameServerStatus.Running, new FakeServers(server), null, whitelist, Ct);

        whitelist.Sincronizados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Outras_transicoes_nao_sincronizam()
    {
        var server = Servidor(GameServerStatus.Stopped);
        var whitelist = new FakeWhitelist();

        await ServerStatusTransitions.ApplyAsync(
            server, GameServerStatus.Starting, new FakeServers(server), null, whitelist, Ct);

        whitelist.Sincronizados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Varredura_do_arranque_aplica_a_whitelist_de_quem_ficou_pronto()
    {
        // O servidor terminou de subir com o painel fora: nenhum evento chegou.
        var server = Servidor(GameServerStatus.Starting);
        var whitelist = new FakeWhitelist();

        await ServerStatusReconciler.ReconcileAsync(
            new FakeServers(server), new FakeOrchestrator(GameServerStatus.Running), whitelist, Ct);

        whitelist.Sincronizados.ShouldBe([server.Id]);
    }

    private sealed class FakeWhitelist : IServerWhitelistSync
    {
        public List<Guid> Sincronizados { get; } = [];

        public Task HandleAsync(Guid gameServerId, CancellationToken ct)
        {
            Sincronizados.Add(gameServerId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActivity : IActivityLogRepository
    {
        public List<ActivityEvent> Registrados { get; } = [];

        public Task AddAsync(ActivityEvent entry, CancellationToken ct)
        {
            Registrados.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ActivityEvent>> ListRecentAsync(int count, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeServers(params GameServer[] seed) : IServerRepository
    {
        public List<Guid> Gravados { get; } = [];

        public Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>(seed);

        public Task UpdateAsync(GameServer server, CancellationToken ct) =>
            throw new InvalidOperationException("o status grava só a coluna dele, nunca a linha inteira");

        public Task UpdateStatusAsync(Guid id, GameServerStatus status, CancellationToken ct)
        {
            Gravados.Add(id);
            return Task.CompletedTask;
        }

        public Task SetContainerIdAsync(Guid id, string? containerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task SetPinnedVersionAsync(Guid id, Guid modpackVersionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task SetCloudVaultAsync(Guid id, Guid? cloudVaultId, CancellationToken ct) =>
            throw new NotImplementedException();

        public List<Guid> MundosMarcados { get; } = [];

        public Task<bool> MarkWorldInitializedAsync(Guid id, DateTimeOffset at, CancellationToken ct)
        {
            MundosMarcados.Add(id);
            return Task.FromResult(true);
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

        public Task<(int Count, long TotalBytes)> GetBackupUsageAsync(CancellationToken ct) =>
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
