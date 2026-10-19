using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Gravações de uma coluna no servidor (6.1). Regressão do problema que a
///     fase veio resolver: quem gravava a linha inteira a partir de uma cópia
///     lida antes desfazia o que outro fluxo tinha gravado no meio — o status de
///     um servidor que subia, o container recém-criado, a versão trocada.
/// </summary>
public sealed class ServerNarrowWritesTests : IDisposable
{
    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";

    private readonly SqliteTestFactory _factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public Task Formulario_com_copia_velha_nao_desfaz_as_colunas_de_escritor_proprio() =>
        FormularioPreserva(_factory);

    [Fact]
    public async Task Formulario_com_copia_velha_nao_desfaz_as_colunas_de_escritor_proprio_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await FormularioPreserva(postgres);
    }

    [Fact]
    public Task Cada_gravacao_estreita_mexe_so_na_sua_coluna() => SoASuaColuna(_factory);

    [Fact]
    public async Task Cada_gravacao_estreita_mexe_so_na_sua_coluna_no_postgres()
    {
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        await SoASuaColuna(postgres);
    }

    private static async Task FormularioPreserva(IDbContextFactory<TcMineDbContext> factory)
    {
        var (server, vault) = await Semear(factory);
        var repo = new ServerRepository(factory, new RecordingResourceChanges());

        // O admin abre o formulário: esta é a cópia que ele vai salvar.
        var formulario = await repo.GetByIdAsync(server.Id, Ct);

        // Enquanto isso, os outros fluxos gravam cada um a sua coluna.
        var versaoNova = Guid.CreateVersion7();
        await repo.UpdateStatusAsync(server.Id, GameServerStatus.Running, Ct);
        await repo.SetContainerIdAsync(server.Id, "container-novo", Ct);
        await repo.SetPinnedVersionAsync(server.Id, versaoNova, Ct);
        await repo.SetCloudVaultAsync(server.Id, vault.Id, Ct);
        await repo.MarkWorldInitializedAsync(server.Id, DateTimeOffset.UtcNow, Ct);

        formulario!.Name = "Nome novo";
        formulario.MemoryMb = 8192;
        await repo.UpdateAsync(formulario, Ct);

        var lido = (await repo.GetByIdAsync(server.Id, Ct))!;
        lido.Name.ShouldBe("Nome novo");
        lido.MemoryMb.ShouldBe(8192);
        lido.Status.ShouldBe(GameServerStatus.Running);
        lido.ContainerId.ShouldBe("container-novo");
        lido.ModpackVersionId.ShouldBe(versaoNova);
        lido.CloudVaultId.ShouldBe(vault.Id);
        lido.HasWorld.ShouldBeTrue();
    }

    private static async Task SoASuaColuna(IDbContextFactory<TcMineDbContext> factory)
    {
        var (server, vault) = await Semear(factory);
        var repo = new ServerRepository(factory, new RecordingResourceChanges());

        // Uma edição "do formulário" no meio, gravada direto no banco.
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            await db.GameServers.Where(s => s.Id == server.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MemoryMb, 6144), Ct);
        }

        var versaoNova = Guid.CreateVersion7();
        await repo.SetContainerIdAsync(server.Id, "c-1", Ct);
        await repo.SetPinnedVersionAsync(server.Id, versaoNova, Ct);
        await repo.SetCloudVaultAsync(server.Id, vault.Id, Ct);

        var lido = (await repo.GetByIdAsync(server.Id, Ct))!;
        lido.MemoryMb.ShouldBe(6144, "nenhuma gravação estreita regravou a memória");
        lido.ContainerId.ShouldBe("c-1");
        lido.ModpackVersionId.ShouldBe(versaoNova);
        lido.CloudVaultId.ShouldBe(vault.Id);
        lido.UpdatedAt.ShouldNotBeNull();

        // Desligar a nuvem é gravar nulo, e o container pode ser esquecido.
        await repo.SetCloudVaultAsync(server.Id, null, Ct);
        await repo.SetContainerIdAsync(server.Id, null, Ct);
        lido = (await repo.GetByIdAsync(server.Id, Ct))!;
        lido.CloudVaultId.ShouldBeNull();
        lido.ContainerId.ShouldBeNull();
    }

    private static async Task<(GameServer, CloudVault)> Semear(IDbContextFactory<TcMineDbContext> factory)
    {
        var dono = Guid.CreateVersion7();
        var vault = new CloudVault { Name = "Nuvem", OwnerId = dono };
        var server = new GameServer
        {
            Name = "Survival",
            ModpackId = Guid.CreateVersion7(),
            ModpackVersionId = Guid.CreateVersion7(),
            ConnectAddress = "",
            GamePort = 25565,
            RconSecret = "segredo",
            OwnerId = dono
        };

        await using var db = await factory.CreateDbContextAsync(Ct);
        db.CloudVaults.Add(vault);
        db.GameServers.Add(server);
        await db.SaveChangesAsync(Ct);
        return (server, vault);
    }
}
