using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     Regressão: até a 0.13.0 nada gravava a marca de mundo, e a troca de versão
///     de um servidor com mundo rodava sem backup. O arranque marca quem já tem
///     mundo no disco, inclusive o servidor parado que ninguém vai ligar antes de
///     trocar a versão.
/// </summary>
public sealed class BackfillWorldInitializedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Servidor_com_mundo_no_disco_e_sem_marca_ganha_a_marca()
    {
        var parado = Servidor();
        var servers = new FakeServers(parado);

        var marcados = await new BackfillWorldInitialized(servers, new FakeWorlds(parado.Id)).HandleAsync(Ct);

        marcados.ShouldBe(1);
        servers.Marcados.ShouldBe([parado.Id]);
    }

    [Fact]
    public async Task Servidor_sem_mundo_no_disco_fica_como_esta()
    {
        // Nunca ligou, ou o primeiro boot caiu antes de gerar o level.dat.
        var novo = Servidor();
        var servers = new FakeServers(novo);

        var marcados = await new BackfillWorldInitialized(servers, new FakeWorlds()).HandleAsync(Ct);

        marcados.ShouldBe(0);
        servers.Marcados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Servidor_ja_marcado_nem_consulta_o_disco()
    {
        // Idempotente: a segunda passada do arranque não faz nada.
        var marcado = Servidor();
        marcado.WorldInitializedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var servers = new FakeServers(marcado);
        var worlds = new FakeWorlds(marcado.Id);

        var marcados = await new BackfillWorldInitialized(servers, worlds).HandleAsync(Ct);

        marcados.ShouldBe(0);
        servers.Marcados.ShouldBeEmpty();
        worlds.Consultados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Marca_que_outro_gravou_antes_nao_conta()
    {
        // O acompanhamento do Docker marcou no mesmo instante: o repositório
        // devolve falso, e o arranque não se gaba de uma marca que não deu.
        var server = Servidor();
        var servers = new FakeServers(server) { OutroJaMarcou = true };

        var marcados = await new BackfillWorldInitialized(servers, new FakeWorlds(server.Id)).HandleAsync(Ct);

        marcados.ShouldBe(0);
    }

    private static GameServer Servidor() => new()
    {
        Name = "Survival",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "",
        RconSecret = "segredo"
    };

    private sealed class FakeServers(params GameServer[] seed) : FakeServerRepositoryBase
    {
        public List<Guid> Marcados { get; } = [];
        public bool OutroJaMarcou { get; init; }

        public override Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>(seed);

        public override Task<bool> MarkWorldInitializedAsync(Guid id, DateTimeOffset at, CancellationToken ct)
        {
            if (OutroJaMarcou)
                return Task.FromResult(false);

            Marcados.Add(id);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeWorlds(params Guid[] comMundo) : IWorldBackupStore
    {
        public List<Guid> Consultados { get; } = [];

        public bool HasWorld(Guid gameServerId)
        {
            Consultados.Add(gameServerId);
            return comMundo.Contains(gameServerId);
        }

        public Task<StoredWorldBackup?> CreateAsync(
            Guid gameServerId, Action<int, int>? onProgress, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> RestoreAsync(
            Guid gameServerId, string fileName, Action<int, int>? onProgress, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> DeleteAsync(Guid gameServerId, string fileName, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<Stream?> OpenAsync(Guid gameServerId, string fileName, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
