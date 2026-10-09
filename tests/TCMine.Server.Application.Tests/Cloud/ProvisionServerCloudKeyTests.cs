using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Cloud;

/// <summary>
///     A chave chega ao servidor de jogo por arquivo, renovada a cada start. A
///     chave anterior para de valer; servidor sem nuvem (ou TCMine sem URL) não
///     fica com arquivo nenhum.
/// </summary>
public sealed class ProvisionServerCloudKeyTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Uri Url = new("https://tcmine.exemplo.com/");

    [Fact]
    public async Task Servidor_na_nuvem_recebe_chave_nova_e_a_anterior_e_revogada()
    {
        var (servidor, repo) = ServidorNaNuvem();
        var files = new FakeCloudServerFiles();
        var chaves = new FakeCloudCredentialRepository();
        var provision = FakeCloud.Provisioner(repo, files, chaves, Url);

        await provision.HandleAsync(servidor.Id, Ct);
        var primeira = files.Files[servidor.Id].Key;
        await provision.HandleAsync(servidor.Id, Ct);
        var segunda = files.Files[servidor.Id].Key;

        segunda.ShouldNotBe(primeira);
        files.Files[servidor.Id].Url.ShouldBe(Url);
        chaves.Credentials.Count(c => c.IsActive).ShouldBe(1, "só a chave do último start vale");
        CloudServerKey.Matches(segunda, chaves.Credentials.Single(c => c.IsActive).KeyHash).ShouldBeTrue();
        chaves.Credentials.Single(c => c.IsActive).VaultId.ShouldBe(servidor.CloudVaultId!.Value);
    }

    [Fact]
    public async Task Servidor_sem_nuvem_fica_sem_arquivo()
    {
        var servidor = NovoServidor(Guid.CreateVersion7());
        var files = new FakeCloudServerFiles();
        files.Files[servidor.Id] = (Url, "velha");

        await FakeCloud.Provisioner(new UmServidor(servidor), files, url: Url).HandleAsync(servidor.Id, Ct);

        files.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_url_configurada_nao_emite_chave()
    {
        var (servidor, repo) = ServidorNaNuvem();
        var files = new FakeCloudServerFiles();
        var chaves = new FakeCloudCredentialRepository();

        await FakeCloud.Provisioner(repo, files, chaves, null).HandleAsync(servidor.Id, Ct);

        files.Files.ShouldBeEmpty();
        chaves.Credentials.ShouldBeEmpty();
    }

    [Fact]
    public async Task Falha_ao_gravar_a_chave_nao_impede_o_servidor_de_subir()
    {
        var (servidor, repo) = ServidorNaNuvem();
        var orchestrator = new StartRecorder();
        var provision = FakeCloud.Provisioner(repo, new FakeCloudServerFiles { Fail = true }, url: Url);

        var result = await new StartGameServer(orchestrator, repo, new FakeJobProgress(), new FakeUserScope(),
                new FakeWhitelistSync(), provision, NullLogger<StartGameServer>.Instance)
            .HandleAsync(servidor.Id, Ct);

        result.Succeeded.ShouldBeTrue();
        orchestrator.Started.ShouldBeTrue();
    }

    private static (GameServer, UmServidor) ServidorNaNuvem()
    {
        var dono = Guid.CreateVersion7();
        var servidor = NovoServidor(dono);
        servidor.AttachToCloudVault(new CloudVault { Name = "N", OwnerId = dono });
        return (servidor, new UmServidor(servidor));
    }

    private static GameServer NovoServidor(Guid dono) => new()
    {
        Name = "S",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "localhost",
        RconSecret = "segredo",
        OwnerId = dono
    };

    private sealed class UmServidor(GameServer servidor) : FakeServerRepositoryBase
    {
        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<GameServer?>(id == servidor.Id ? servidor : null);

        public override Task UpdateAsync(GameServer server, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StartRecorder : IServerOrchestrator
    {
        public bool Started { get; private set; }

        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) => Task.FromResult("c");

        public Task StartAsync(Guid gameServerId, CancellationToken ct)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) => Task.CompletedTask;

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult(GameServerStatus.Running);

        public IAsyncEnumerable<ConsoleLine> StreamLogsAsync(Guid gameServerId, CancellationToken ct) =>
            AsyncEnumerable.Empty<ConsoleLine>();

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) => Task.CompletedTask;
    }
}
