using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Hubs;

namespace TCMine.Server.Web.Tests.Hubs;

/// <summary>
///     Ciclo de vida do bombeamento do console.
///     O que se testa aqui é contagem, e a razão é concreta: cada stream é uma
///     conexão HTTP permanente com o daemon do Docker. Errar para mais abre
///     leituras duplicadas dos mesmos bytes; errar para menos deixa o stream
///     aberto para ninguém, até o processo reiniciar.
/// </summary>
public sealed class ConsoleBroadcasterTests
{
    private static readonly Guid ServidorId = Guid.CreateVersion7();

    [Fact]
    public async Task Dez_ouvintes_do_mesmo_servidor_abrem_um_stream_so()
    {
        // O grupo do SignalR já faz o fan-out; um stream por conexão seria dez
        // leituras dos mesmos bytes no daemon.
        var orchestrator = new FakeOrchestrator();
        await using var broadcaster = Novo(orchestrator);

        for (var i = 0; i < 10; i++)
            broadcaster.Subscribe($"conexao-{i}", ServidorId);

        await orchestrator.AguardarAberturasAsync(1);

        orchestrator.Aberturas.ShouldBe(1);
    }

    [Fact]
    public async Task Stream_fecha_quando_o_ultimo_ouvinte_sai()
    {
        var orchestrator = new FakeOrchestrator();
        await using var broadcaster = Novo(orchestrator);

        broadcaster.Subscribe("a", ServidorId);
        broadcaster.Subscribe("b", ServidorId);
        await orchestrator.AguardarAberturasAsync(1);

        broadcaster.Unsubscribe("a", ServidorId);
        orchestrator.Cancelados.ShouldBe(0, "ainda há quem ouça");

        broadcaster.Unsubscribe("b", ServidorId);
        await orchestrator.AguardarCancelamentosAsync(1);

        orchestrator.Cancelados.ShouldBe(1);
    }

    [Fact]
    public async Task Queda_de_conexao_solta_tudo_o_que_ela_segurava()
    {
        // O caso comum: o jogador fecha o launcher sem avisar. Sem isto o
        // contador nunca voltaria a zero.
        var outro = Guid.CreateVersion7();
        var orchestrator = new FakeOrchestrator();
        await using var broadcaster = Novo(orchestrator);

        broadcaster.Subscribe("a", ServidorId);
        broadcaster.Subscribe("a", outro);
        await orchestrator.AguardarAberturasAsync(2);

        broadcaster.Disconnect("a");
        await orchestrator.AguardarCancelamentosAsync(2);

        orchestrator.Cancelados.ShouldBe(2);
    }

    [Fact]
    public async Task Assinar_duas_vezes_na_mesma_conexao_nao_conta_dobrado()
    {
        // Se contasse, um único Unsubscribe deixaria o contador em 1 para
        // sempre e o stream jamais fecharia.
        var orchestrator = new FakeOrchestrator();
        await using var broadcaster = Novo(orchestrator);

        broadcaster.Subscribe("a", ServidorId);
        broadcaster.Subscribe("a", ServidorId);
        await orchestrator.AguardarAberturasAsync(1);

        broadcaster.Unsubscribe("a", ServidorId);
        await orchestrator.AguardarCancelamentosAsync(1);

        orchestrator.Cancelados.ShouldBe(1);
    }

    private static ConsoleBroadcaster Novo(FakeOrchestrator orchestrator)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServerOrchestrator>(orchestrator);
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();

        // A entrega vai pela porta IServerHubNotifier, e não pelo
        // LauncherNotifier concreto: é o que permite testar o ciclo de vida do
        // stream sem subir um hub inteiro.
        services.AddSingleton<IServerHubNotifier, FakeNotifier>();

        return new ConsoleBroadcaster(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ConsoleBroadcaster>.Instance);
    }

    private sealed class FakeNotifier : IServerHubNotifier
    {
        public Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task NotifyConsoleLineAsync(Guid serverId, ConsoleLineDto line, CancellationToken ct) =>
            Task.CompletedTask;

        public Task NotifyPlayerCountChangedAsync(Guid serverId, int online, int max, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeOrchestrator : IServerOrchestrator
    {
        private readonly Lock _gate = new();
        public int Aberturas { get; private set; }
        public int Cancelados { get; private set; }

        public async IAsyncEnumerable<ConsoleLine> StreamLogsAsync(
            Guid gameServerId,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct)
        {
            lock (_gate)
            {
                Aberturas++;
            }

            try
            {
                // Nunca produz linha: o teste é sobre abrir e fechar. Uma linha
                // exigiria o notificador de verdade.
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally
            {
                lock (_gate)
                {
                    Cancelados++;
                }
            }

            yield break;
        }

        public async Task AguardarAberturasAsync(int quantas) =>
            await AguardarAsync(() => Aberturas >= quantas);

        public async Task AguardarCancelamentosAsync(int quantos) =>
            await AguardarAsync(() => Cancelados >= quantos);

        /// <summary>
        ///     Espera curta em laço em vez de um sleep fixo: o bombeamento roda
        ///     em outra tarefa, e um atraso arbitrário deixaria o teste instável
        ///     nas duas direções — lento quando passa, intermitente quando não.
        /// </summary>
        private static async Task AguardarAsync(Func<bool> condicao)
        {
            var limite = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < limite)
            {
                if (condicao())
                    return;

                await Task.Delay(20);
            }
        }

        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StartAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult(GameServerStatus.Running);

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
