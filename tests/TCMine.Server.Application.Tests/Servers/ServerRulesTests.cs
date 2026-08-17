using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     Guardas de criação, troca de versão e remoção. São as regras que impedem
///     um servidor de subir com um pack quebrado ou um mundo de ser corrompido.
/// </summary>
public sealed class ServerRulesTests
{
    private readonly Guid _modpackId = Guid.CreateVersion7();

    [Fact]
    public async Task Nao_cria_servidor_sem_versao_publicada()
    {
        // Rascunho não tem os arquivos resolvidos: o container subiria sem mods.
        var rascunho = Versao("1.0.0", ModpackVersionState.Draft);

        var result = await NewCreate(rascunho).HandleAsync(
            _modpackId, "Servidor", "jogo:25565", 4096, 20, Guid.Empty, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Nao_cria_servidor_fixado_em_pre_release()
    {
        // Alpha é onde os mods ainda partem o servidor. Publicada não basta:
        // tem de ser estável.
        var alpha = Versao("1.0.0-alpha", ModpackVersionState.Ready);

        var result = await NewCreate(alpha).HandleAsync(
            _modpackId, "Servidor", "jogo:25565", 4096, 20, Guid.Empty, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Cria_servidor_com_segredo_rcon_proprio()
    {
        var estavel = Versao("1.0.0", ModpackVersionState.Ready);
        var servers = new FakeServers();

        var result = await NewCreate(estavel, servers).HandleAsync(
            _modpackId, "  Servidor  ", " jogo:25565 ", 4096, 20, Guid.Empty, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Servidor", servers.Adicionado!.Name);
        Assert.Equal("jogo:25565", servers.Adicionado.ConnectAddress);

        // Quem tem a senha RCON manda na máquina do jogo: ela nasce aqui, forte
        // e diferente a cada servidor — nunca vem do formulário.
        Assert.True(servers.Adicionado.RconSecret.Length >= 32);
    }

    [Fact]
    public async Task Quem_cria_o_servidor_vira_Owner_dele()
    {
        // Sem este vínculo o servidor nasceria sem ninguém que pudesse convidar
        // ou apagá-lo: o OwnerId é costura de multi-tenant, quem decide
        // permissão é o Membership.
        var memberships = new FakeMemberships();
        var version = Versao("1.0.0", ModpackVersionState.Ready);

        var result = await NewCreate(version, memberships: memberships)
            .HandleAsync(version.ModpackId, "Survival", "jogo:25565", 4096, 20, version.Id,
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(memberships.Adicionado);
        Assert.Equal(ServerRole.Owner, memberships.Adicionado!.Role);
        Assert.Equal(result.Value, memberships.Adicionado.GameServerId);
    }

    [Fact]
    public async Task Moderador_nao_muda_a_configuracao_do_servidor()
    {
        // Nome, endereço, RAM e limite de jogadores. Não é destrutivo, mas
        // trocar o ConnectAddress redireciona todo mundo para outra máquina.
        var server = Servidor();
        var servers = new FakeServers(server);

        var result = await new UpdateGameServer(servers, new FakeUserScope(ServerRoleDto.Moderator))
            .HandleAsync(server.Id, "outro nome", "outro:25565", 4096, 20, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Servidor não encontrado.", result.Error);
    }

    [Fact]
    public async Task Apagar_servidor_exige_Owner_e_Admin_nao_basta()
    {
        // A única ação da lista sem volta: leva o mundo junto e nenhum backup
        // automático a precede. Admin cuida da operação do dia a dia; encerrar
        // o servidor é decisão de dono.
        var server = Servidor();
        var servers = new FakeServers(server);
        var orchestrator = new FakeOrchestrator();
        var materializer = new FakeMaterializer();

        var result = await new DeleteGameServer(
                servers, orchestrator, materializer, new FakeJobProgress(),
                new FakeUserScope(ServerRoleDto.Admin))
            .HandleAsync(server.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(servers.Removido);
        Assert.False(orchestrator.Removido);
        Assert.False(materializer.Apagado);
    }

    [Fact]
    public async Task Apagar_servidor_remove_container_e_pasta_antes_do_registro()
    {
        var server = Servidor();
        var servers = new FakeServers(server);
        var orchestrator = new FakeOrchestrator();
        var materializer = new FakeMaterializer();

        var result = await new DeleteGameServer(servers, orchestrator, materializer, new FakeJobProgress(), new FakeUserScope())
            .HandleAsync(server.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(orchestrator.Removido);
        Assert.True(materializer.Apagado);
        Assert.True(servers.Removido);
    }

    [Fact]
    public async Task Falha_ao_remover_o_container_mantem_o_registro()
    {
        // Apagar a linha com o container de pé deixaria um servidor rodando que
        // o painel não conhece mais — impossível de parar pela UI.
        var server = Servidor();
        var servers = new FakeServers(server);
        var orchestrator = new FakeOrchestrator { Explode = true };

        var result = await new DeleteGameServer(servers, orchestrator, new FakeMaterializer(), new FakeJobProgress(), new FakeUserScope())
            .HandleAsync(server.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(servers.Removido);
    }

    // ---- Fixtures ----

    private static CreateGameServer NewCreate(
        ModpackVersion version, FakeServers? servers = null, FakeMemberships? memberships = null) =>
        new(servers ?? new FakeServers(), new FakeModpacks(version),
            memberships ?? new FakeMemberships(), new FakeScope());

    private ModpackVersion Versao(string numero, ModpackVersionState estado)
    {
        var version = new ModpackVersion
        {
            ModpackId = _modpackId, Version = numero, LoaderVersion = "21.1.100"
        };

        if (estado is ModpackVersionState.Ready)
        {
            version.UpsertFile(new ModpackFile
            {
                ModpackVersionId = version.Id,
                Path = "mods/x.jar",
                Sha256 = new string('a', 64),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.Modrinth,
                ProjectSlug = "x"
            });

            version.MarkResolving();
            version.MarkReady();
        }

        return version;
    }

    private GameServer Servidor() => new()
    {
        Name = "Servidor",
        ModpackId = _modpackId,
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogo:25565",
        RconSecret = "segredo"
    };

    // ---- Fakes ----

    private sealed class FakeModpacks(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ModpackVersion>>([version]);

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version.Id == versionId ? version : null);
    }

    private sealed class FakeServers(params GameServer[] seed) : FakeServerRepositoryBase
    {
        private readonly List<GameServer> _servers = [.. seed];

        public GameServer? Adicionado { get; private set; }
        public bool Removido { get; private set; }

        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_servers.FirstOrDefault(s => s.Id == id));

        public override Task AddAsync(GameServer server, CancellationToken ct)
        {
            Adicionado = server;
            return Task.CompletedTask;
        }

        public override Task UpdateAsync(GameServer server, CancellationToken ct) => Task.CompletedTask;

        public override Task RemoveAsync(Guid id, CancellationToken ct)
        {
            Removido = true;
            return Task.CompletedTask;
        }

    }

    private sealed class FakeOrchestrator : IServerOrchestrator
    {
        public bool Explode { get; init; }
        public bool Removido { get; private set; }

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct)
        {
            if (Explode)
                throw new InvalidOperationException("docker fora do ar");

            Removido = true;
            return Task.CompletedTask;
        }

        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StartAsync(Guid gameServerId, CancellationToken ct) => throw new NotImplementedException();

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<ConsoleLine> StreamLogsAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeMaterializer : IInstanceMaterializer
    {
        public bool Apagado { get; private set; }

        public Task DeleteInstanceAsync(Guid gameServerId, CancellationToken ct)
        {
            Apagado = true;
            return Task.CompletedTask;
        }

        public Task MaterializeAsync(Guid gameServerId, ModpackVersion version, CancellationToken ct) =>
            throw new NotImplementedException();

        public string GetInstancePath(Guid gameServerId) => throw new NotImplementedException();
    }

    private sealed class FakeScope : ICurrentUserScope
    {
        public Guid? UserId => Guid.Empty;
        public Guid OwnerId => Guid.Empty;
        public bool IsInstanceAdmin => true;

        public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult<ServerRoleDto?>(ServerRoleDto.Owner);
    }
}
