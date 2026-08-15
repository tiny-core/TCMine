using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Rastro e retomada das importações.
///     Esta era a última falha invisível do módulo: a importação passa minutos
///     baixando e lendo o zip do pack ANTES de gravar a primeira linha, e uma
///     queda nesse trecho não deixava vestígio nenhum — a barra sumia e nada
///     tinha acontecido, sem nem registro de que alguém pediu.
///     A regra que sustenta tudo: quem termina apaga a linha, dê certo ou dê
///     errado. Linha que sobrou no arranque = processo morreu no meio.
/// </summary>
public sealed class ImportRecoveryTests
{
    [Fact]
    public async Task Pedido_e_gravado_antes_de_entrar_na_fila()
    {
        var repo = new FakeRequests();
        var queue = new FakeQueue(repo);

        var result = await new ImportScheduler(repo, queue).ScheduleAsync(
            ModFileOrigin.CurseForge, "999", null, "All the Mods 10", CancellationToken.None);

        result.Succeeded.ShouldBeTrue();

        // A ordem é a garantia: invertida, existe uma janela em que o job está na
        // fila sem rastro no banco — exatamente o buraco que isto veio fechar.
        repo.Eventos.ShouldBe(["gravou", "enfileirou"]);

        var gravado = repo.Registros.Single();
        gravado.ProjectId.ShouldBe("999");
        gravado.DisplayName.ShouldBe("All the Mods 10");
        result.Value.ShouldBe(gravado.Id);
    }

    [Fact]
    public async Task Mesmo_pack_nao_entra_duas_vezes_ao_mesmo_tempo()
    {
        var repo = new FakeRequests();
        var queue = new FakeQueue(repo);
        var scheduler = new ImportScheduler(repo, queue);

        await scheduler.ScheduleAsync(ModFileOrigin.CurseForge, "999", null, "Pack", CancellationToken.None);
        var segunda = await scheduler.ScheduleAsync(
            ModFileOrigin.CurseForge, "999", null, "Pack", CancellationToken.None);

        // Duas importações do mesmo pack criariam dois modpacks disputando a
        // mesma procedência, e a detecção de atualização não saberia qual seguir.
        segunda.Succeeded.ShouldBeFalse();
        queue.Enfileirados.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Rastro_que_sobrou_volta_para_a_fila_no_arranque()
    {
        var repo = new FakeRequests();
        repo.Registros.Add(new ImportRequest
        {
            Origin = ModFileOrigin.CurseForge, ProjectId = "999", DisplayName = "Pack"
        });
        var queue = new FakeQueue(repo);

        var retomadas = await new RecoverInterruptedImports(repo, queue).HandleAsync(CancellationToken.None);

        retomadas.ShouldBe(1);
        queue.Enfileirados.Single().ProjectId.ShouldBe("999");
        repo.Registros.Single().RecoveryAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task Depois_do_limite_o_rastro_e_apagado_em_vez_de_repetir()
    {
        var repo = new FakeRequests();
        repo.Registros.Add(new ImportRequest
        {
            Origin = ModFileOrigin.CurseForge,
            ProjectId = "999",
            DisplayName = "Pack",
            RecoveryAttempts = ImportRequest.MaxRecoveryAttempts
        });
        var queue = new FakeQueue(repo);

        var retomadas = await new RecoverInterruptedImports(repo, queue).HandleAsync(CancellationToken.None);

        retomadas.ShouldBe(0);
        queue.Enfileirados.ShouldBeEmpty();

        // A linha PRECISA sair: mantida, a checagem de duplicata bloquearia o
        // pack para sempre e o admin não conseguiria nem tentar de novo a mão.
        repo.Registros.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_rastro_nao_ha_o_que_retomar()
    {
        var repo = new FakeRequests();
        var queue = new FakeQueue(repo);

        var retomadas = await new RecoverInterruptedImports(repo, queue).HandleAsync(CancellationToken.None);

        retomadas.ShouldBe(0);
        queue.Enfileirados.ShouldBeEmpty();
    }

    private sealed class FakeRequests : IImportRequestRepository
    {
        public List<ImportRequest> Registros { get; } = [];

        public List<string> Eventos { get; } = [];

        public Task AddAsync(ImportRequest request, CancellationToken ct)
        {
            Eventos.Add("gravou");
            Registros.Add(request);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ImportRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveAsync(Guid requestId, CancellationToken ct)
        {
            Registros.RemoveAll(r => r.Id == requestId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ImportRequest>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ImportRequest>>([.. Registros]);

        public Task<bool> ExistsForAsync(ModFileOrigin origin, string projectId, CancellationToken ct) =>
            Task.FromResult(Registros.Any(r => r.Origin == origin && r.ProjectId == projectId));
    }

    private sealed class FakeQueue(FakeRequests repo) : IImportQueue
    {
        public List<ImportRequest> Enfileirados { get; } = [];

        public ValueTask EnqueueAsync(ImportRequest request, CancellationToken ct)
        {
            repo.Eventos.Add("enfileirou");
            Enfileirados.Add(request);
            return ValueTask.CompletedTask;
        }
    }
}
