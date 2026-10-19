using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Instances;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A marca de que o servidor tem mundo (<c>WorldInitializedAt</c>).
///     Regressão: até a 0.13.0 ninguém a gravava, e a troca de versão de um
///     servidor com mundo rodava sem backup. Aqui se cobra o que a torna
///     confiável: grava uma vez só, a primeira data fica, e a gravação da linha
///     inteira (formulário, troca de versão) nunca a apaga.
/// </summary>
public sealed class ServerWorldMarkTests : IDisposable
{
    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private readonly SqliteTestFactory _factory = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tcmine-world-{Guid.CreateVersion7():N}");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public Task Marca_grava_uma_vez_e_a_primeira_data_fica() => MarcaUmaVez(_factory);

    [Fact]
    public async Task Marca_grava_uma_vez_e_a_primeira_data_fica_no_postgres()
    {
        // O "só se vazia" é um IS NULL no WHERE do UPDATE: é na tradução de
        // cada banco que ele poderia virar outra coisa.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await MarcaUmaVez(postgres);
    }

    [Fact]
    public Task Gravar_a_linha_inteira_com_copia_velha_nao_apaga_a_marca() =>
        LinhaInteiraPreservaMarca(_factory);

    [Fact]
    public async Task Gravar_a_linha_inteira_com_copia_velha_nao_apaga_a_marca_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await LinhaInteiraPreservaMarca(postgres);
    }

    [Fact]
    public void Mundo_no_disco_e_o_level_dat_do_mundo_principal()
    {
        var store = new FileSystemWorldBackupStore(
            new PastaFixa(_root), NullLogger<FileSystemWorldBackupStore>.Instance);
        var semPasta = Guid.CreateVersion7();
        var pastaVazia = Guid.CreateVersion7();
        var comMundo = Guid.CreateVersion7();

        // O itzg cria world/ antes de o jogo gerar alguma coisa: pasta sem
        // level.dat não é mundo.
        Directory.CreateDirectory(Path.Combine(_root, pastaVazia.ToString(), "world"));
        Directory.CreateDirectory(Path.Combine(_root, comMundo.ToString(), "world"));
        File.WriteAllBytes(Path.Combine(_root, comMundo.ToString(), "world", "level.dat"), [1]);

        store.HasWorld(semPasta).ShouldBeFalse();
        store.HasWorld(pastaVazia).ShouldBeFalse();
        store.HasWorld(comMundo).ShouldBeTrue();
    }

    private static async Task MarcaUmaVez(IDbContextFactory<TcMineDbContext> factory)
    {
        var server = Servidor();
        await Gravar(factory, server);
        var repo = new ServerRepository(factory, new RecordingResourceChanges());
        var primeira = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        (await repo.MarkWorldInitializedAsync(server.Id, primeira, Ct)).ShouldBeTrue();
        (await repo.MarkWorldInitializedAsync(server.Id, primeira.AddDays(5), Ct))
            .ShouldBeFalse("já estava marcado");

        var lido = await repo.GetByIdAsync(server.Id, Ct);
        lido!.WorldInitializedAt.ShouldBe(primeira);
    }

    private static async Task LinhaInteiraPreservaMarca(IDbContextFactory<TcMineDbContext> factory)
    {
        var server = Servidor();
        await Gravar(factory, server);
        var repo = new ServerRepository(factory, new RecordingResourceChanges());

        // O formulário leu o servidor ANTES de ele entrar em Running.
        var copiaVelha = await repo.GetByIdAsync(server.Id, Ct);
        await repo.MarkWorldInitializedAsync(server.Id, DateTimeOffset.UtcNow, Ct);

        copiaVelha!.MemoryMb = 6144;
        await repo.UpdateAsync(copiaVelha, Ct);

        var lido = await repo.GetByIdAsync(server.Id, Ct);
        lido!.MemoryMb.ShouldBe(6144, "o resto da linha grava normalmente");
        lido.HasWorld.ShouldBeTrue("a cópia velha não tinha a marca, e ela não pode sumir");
    }

    private static async Task Gravar(IDbContextFactory<TcMineDbContext> factory, GameServer server)
    {
        await using var db = await factory.CreateDbContextAsync(Ct);
        db.GameServers.Add(server);
        await db.SaveChangesAsync(Ct);
    }

    private static GameServer Servidor() => new()
    {
        Name = "Survival",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "",
        GamePort = 25565,
        RconSecret = "segredo"
    };

    private sealed class PastaFixa(string root) : IInstanceMaterializer
    {
        public Task MaterializeAsync(Guid gameServerId, ModpackVersion version, CancellationToken ct) =>
            throw new NotSupportedException();

        public string GetInstancePath(Guid gameServerId) => Path.Combine(root, gameServerId.ToString());

        public Task DeleteInstanceAsync(Guid gameServerId, CancellationToken ct) => throw new NotSupportedException();
    }
}
