using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Endpoints;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Testes de regressão da autorização do download de backup.
///     O endpoint já nasceu apenas com RequireAuthorization(), o que deixava
///     qualquer usuário do painel baixar o mundo de qualquer servidor sabendo os
///     dois GUIDs. É uma falha silenciosa por natureza — nada quebra, nada loga,
///     o arquivo simplesmente sai. Estes testes travam o guard no lugar.
/// </summary>
public class WorldBackupEndpointsTests
{
    private static readonly Guid ServerId = Guid.CreateVersion7();
    private static readonly Guid BackupId = Guid.CreateVersion7();

    [Fact]
    public async Task Sem_vinculo_com_o_servidor_devolve_404_sem_tocar_no_arquivo()
    {
        var store = new FakeBackupStore();

        var result = await InvokeAsync(role: null, store: store);

        result.ShouldBeOfType<NotFound>();

        // A asserção que importa de verdade: nenhum byte foi aberto. Um 404 que
        // ainda assim abrisse o stream continuaria sendo vazamento.
        store.Opened.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ServerRoleDto.Member)]
    [InlineData(ServerRoleDto.Moderator)]
    public async Task Papel_abaixo_de_admin_devolve_404(ServerRoleDto role)
    {
        var store = new FakeBackupStore();

        var result = await InvokeAsync(role, store);

        result.ShouldBeOfType<NotFound>();
        store.Opened.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ServerRoleDto.Admin)]
    [InlineData(ServerRoleDto.Owner)]
    public async Task Admin_e_owner_baixam_o_arquivo(ServerRoleDto role)
    {
        var store = new FakeBackupStore();

        var result = await InvokeAsync(role, store);

        result.ShouldBeOfType<FileStreamHttpResult>();
        store.Opened.ShouldBeTrue();
    }

    [Fact]
    public async Task Backup_de_outro_servidor_devolve_404()
    {
        var store = new FakeBackupStore();

        // Backup existe, o usuário é Owner — mas pertence a outro servidor. A
        // rota não pode servir de atalho para o mundo do vizinho.
        var repository = new FakeServerRepository(BackupOf(Guid.CreateVersion7()));

        var result = await WorldBackupEndpoints.DownloadAsync(
            ServerId, BackupId, new FakeUserScope(ServerRoleDto.Owner), repository, store,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NotFound>();
        store.Opened.ShouldBeFalse();
    }

    [Fact]
    public async Task Papel_e_consultado_para_o_servidor_da_rota()
    {
        var scope = new FakeUserScope(ServerRoleDto.Owner);

        await WorldBackupEndpoints.DownloadAsync(
            ServerId, BackupId, scope, new FakeServerRepository(BackupOf(ServerId)),
            new FakeBackupStore(), TestContext.Current.CancellationToken);

        // Guard contra a inversão de argumentos: perguntar o papel usando o id do
        // backup passaria em todo teste acima e liberaria tudo em produção.
        scope.AskedFor.ShouldBe(ServerId);
    }

    private static Task<IResult> InvokeAsync(ServerRoleDto? role, FakeBackupStore store) =>
        WorldBackupEndpoints.DownloadAsync(
            ServerId,
            BackupId,
            new FakeUserScope(role),
            new FakeServerRepository(BackupOf(ServerId)),
            store,
            TestContext.Current.CancellationToken);

    private static WorldBackup BackupOf(Guid gameServerId) => new()
    {
        GameServerId = gameServerId,
        FileName = "mundo-2026-08-15.zip",
        SizeBytes = 1024,
        Reason = WorldBackupReason.Manual
    };

    private sealed class FakeServerRepository(WorldBackup backup) : FakeServerRepositoryBase
    {
        public override Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct) =>
            Task.FromResult<WorldBackup?>(backup);
    }

    private sealed class FakeUserScope(ServerRoleDto? role) : ICurrentUserScope
    {
        public Guid? AskedFor { get; private set; }

        public Guid? UserId => Guid.Empty;

        public Guid OwnerId => Guid.Empty;

        public bool IsInstanceAdmin => false;

        public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct)
        {
            AskedFor = gameServerId;
            return Task.FromResult(role);
        }
    }

    private sealed class FakeBackupStore : IWorldBackupStore
    {
        public bool Opened { get; private set; }

        public Task<Stream?> OpenAsync(Guid gameServerId, string fileName, CancellationToken ct)
        {
            Opened = true;
            return Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        }

        public Task<StoredWorldBackup?> CreateAsync(
            Guid gameServerId, Action<int, int>? onProgress, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> RestoreAsync(
            Guid gameServerId, string fileName, Action<int, int>? onProgress, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> DeleteAsync(Guid gameServerId, string fileName, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
