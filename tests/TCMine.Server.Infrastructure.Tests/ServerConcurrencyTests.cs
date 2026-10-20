using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Token de concorrência do servidor (6.3). O que a fase exige: de duas
///     gravações do formulário a partir da mesma leitura, a segunda falha em vez
///     de sobrescrever a primeira. E o que a 6.1 garantiu continua valendo: uma
///     gravação de uma coluna no meio (ligar o servidor) não faz o formulário
///     falhar.
/// </summary>
public sealed class ServerConcurrencyTests : IDisposable
{
    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private readonly SqliteTestFactory _factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public Task Segunda_gravacao_da_mesma_leitura_falha_e_nao_sobrescreve() => SegundaFalha(_factory);

    [Fact]
    public async Task Segunda_gravacao_da_mesma_leitura_falha_e_nao_sobrescreve_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await SegundaFalha(postgres);
    }

    [Fact]
    public Task Gravacao_de_uma_coluna_no_meio_nao_faz_o_formulario_falhar() => ColunaNoMeio(_factory);

    [Fact]
    public async Task Gravacao_de_uma_coluna_no_meio_nao_faz_o_formulario_falhar_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await ColunaNoMeio(postgres);
    }

    private static async Task SegundaFalha(IDbContextFactory<TcMineDbContext> factory)
    {
        var server = await Semear(factory);
        var repo = new ServerRepository(factory, new RecordingResourceChanges());

        // Ana e Beto abrem o mesmo formulário.
        var ana = (await repo.GetByIdAsync(server.Id, Ct))!;
        var beto = (await repo.GetByIdAsync(server.Id, Ct))!;

        ana.MemoryMb = 6144;
        (await repo.UpdateAsync(ana, Ct)).ShouldBeTrue();
        ana.RowVersion.ShouldBe(beto.RowVersion + 1, "a cópia de quem gravou acompanha o banco");

        beto.Name = "Nome do Beto";
        (await repo.UpdateAsync(beto, Ct)).ShouldBeFalse("o Beto salvaria por cima da Ana");
        beto.RowVersion.ShouldBe(ana.RowVersion - 1, "a cópia recusada continua dizendo o que leu");

        var lido = (await repo.GetByIdAsync(server.Id, Ct))!;
        lido.MemoryMb.ShouldBe(6144);
        lido.Name.ShouldBe("Survival", "nada do salvar recusado foi gravado");

        // Reaberto, o formulário do Beto grava.
        lido.Name = "Nome do Beto";
        (await repo.UpdateAsync(lido, Ct)).ShouldBeTrue();
    }

    private static async Task ColunaNoMeio(IDbContextFactory<TcMineDbContext> factory)
    {
        var server = await Semear(factory);
        var repo = new ServerRepository(factory, new RecordingResourceChanges());

        var formulario = (await repo.GetByIdAsync(server.Id, Ct))!;
        await repo.UpdateStatusAsync(server.Id, GameServerStatus.Running, Ct);
        await repo.SetContainerIdAsync(server.Id, "c-1", Ct);

        formulario.MaxPlayers = 40;
        (await repo.UpdateAsync(formulario, Ct)).ShouldBeTrue("ligar o servidor não mexe no que o formulário edita");

        var lido = (await repo.GetByIdAsync(server.Id, Ct))!;
        lido.MaxPlayers.ShouldBe(40);
        lido.Status.ShouldBe(GameServerStatus.Running);
    }

    private static async Task<GameServer> Semear(IDbContextFactory<TcMineDbContext> factory)
    {
        var server = new GameServer
        {
            Name = "Survival",
            ModpackId = Guid.CreateVersion7(),
            ModpackVersionId = Guid.CreateVersion7(),
            ConnectAddress = "",
            GamePort = 25565,
            RconSecret = "segredo"
        };

        await using var db = await factory.CreateDbContextAsync(Ct);
        db.GameServers.Add(server);
        await db.SaveChangesAsync(Ct);
        return server;
    }
}
