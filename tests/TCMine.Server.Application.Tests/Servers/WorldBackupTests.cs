using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     O backup é o que torna a troca de versão reversível. Cada guarda aqui
///     existe para o snapshot não sair inútil — um arquivo que parece backup e
///     não restaura é pior que não ter backup nenhum.
/// </summary>
public sealed class WorldBackupTests
{
    [Fact]
    public async Task Backup_a_quente_pausa_o_autosave_descarrega_copia_e_religa()
    {
        // A ordem é o contrato: save-off antes do flush (senão o jogo volta a
        // escrever no instante seguinte), e save-on por último, sempre.
        var server = Servidor();
        var repo = new FakeServers(server);
        var rcon = new FakeRcon();
        var store = new FakeStore();

        var result = await NewBackup(server, store, GameServerStatus.Running, repo, rcon: rcon)
            .HandleAsync(server.Id, null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["save-off", "save-all flush", "save-on"], rcon.Comandos);
        Assert.True(store.Criou);
        Assert.True(repo.Adicionado!.TakenHot);
    }

    [Fact]
    public async Task Religa_o_autosave_mesmo_quando_a_copia_falha()
    {
        // Deixar save-off ligado é pior que não ter backup: o servidor roda sem
        // persistir, e a próxima queda leva tudo desde então.
        var server = Servidor();
        var rcon = new FakeRcon();

        var result = await NewBackup(
                server, new FakeStore { Explode = true }, GameServerStatus.Running, rcon: rcon)
            .HandleAsync(server.Id, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("save-on", rcon.Comandos);
    }

    [Fact]
    public async Task Nao_copia_as_cegas_quando_o_jogo_nao_responde()
    {
        var server = Servidor();
        var store = new FakeStore();

        var result = await NewBackup(
                server, store, GameServerStatus.Running, rcon: new FakeRcon { Indisponivel = true })
            .HandleAsync(server.Id, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(store.Criou);
    }

    [Fact]
    public async Task Autosave_que_nao_volta_estoura_para_o_admin_saber()
    {
        // A única falha deste caso de uso que exige ação imediata: silenciá-la
        // deixaria o servidor rodando sem gravar nada, e ninguém saberia.
        var server = Servidor();

        await Assert.ThrowsAsync<RconUnavailableException>(() =>
            NewBackup(server, new FakeStore(), GameServerStatus.Running,
                    rcon: new FakeRcon { FalhaNoSaveOn = true })
                .HandleAsync(server.Id, null, CancellationToken.None));
    }

    [Fact]
    public async Task Recusa_backup_com_o_servidor_em_transicao()
    {
        // Iniciando ou parando: nem o caminho a frio nem o a quente valem.
        var server = Servidor();
        var store = new FakeStore();

        var result = await NewBackup(server, store, GameServerStatus.Starting)
            .HandleAsync(server.Id, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(store.Criou);
    }

    [Fact]
    public async Task Backup_a_frio_nao_manda_comando_nenhum()
    {
        var server = Servidor();
        var rcon = new FakeRcon();

        await NewBackup(server, new FakeStore(), GameServerStatus.Stopped, rcon: rcon)
            .HandleAsync(server.Id, null, CancellationToken.None);

        // Servidor parado não tem a quem mandar comando — e tentar daria erro.
        Assert.Empty(rcon.Comandos);
    }

    [Fact]
    public async Task Registra_o_backup_com_a_versao_que_estava_fixada()
    {
        // Sem saber de qual versão o mundo veio, restaurar depois é adivinhação.
        var server = Servidor();
        var repo = new FakeServers(server);

        var result = await NewBackup(server, new FakeStore(), GameServerStatus.Stopped, repo)
            .HandleAsync(server.Id, "  antes de mexer  ", CancellationToken.None);

        Assert.True(result.Succeeded);

        var backup = repo.Adicionado!;
        Assert.Equal(server.ModpackVersionId, backup.ModpackVersionId);
        Assert.Equal("1.0.0", backup.ModpackVersionLabel);
        Assert.Equal("antes de mexer", backup.Note);
    }

    [Fact]
    public async Task Servidor_sem_mundo_nao_gera_backup_vazio()
    {
        var server = Servidor();
        var repo = new FakeServers(server);

        var result = await NewBackup(server, new FakeStore { SemMundo = true }, GameServerStatus.Stopped, repo)
            .HandleAsync(server.Id, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Adicionado);
    }

    [Fact]
    public async Task Troca_de_versao_com_mundo_faz_backup_antes_de_repontar()
    {
        var server = Servidor();
        server.WorldInitializedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var repo = new FakeServers(server);
        var store = new FakeStore();
        var destino = Versao("2.0.0");

        var result = await NewChange(repo, store, destino, GameServerStatus.Stopped)
            .HandleAsync(server.Id, destino.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(store.Criou);
        Assert.Equal(destino.Id, server.ModpackVersionId);
        Assert.Equal(WorldBackupReason.BeforeVersionChange, repo.Adicionado!.Reason);
    }

    [Fact]
    public async Task Backup_que_falha_cancela_a_troca_de_versao()
    {
        // O ponteiro só muda depois do snapshot. Trocar sem ele devolveria a
        // operação ao estado irreversível que a fatia veio resolver.
        var server = Servidor();
        server.WorldInitializedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var repo = new FakeServers(server);
        var destino = Versao("2.0.0");
        var original = server.ModpackVersionId;

        var result = await NewChange(repo, new FakeStore { Explode = true }, destino, GameServerStatus.Stopped)
            .HandleAsync(server.Id, destino.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(original, server.ModpackVersionId);
    }

    [Fact]
    public async Task Nao_troca_a_versao_com_o_servidor_rodando_e_mundo_gerado()
    {
        // Aqui a exigência de parado continua, e não é sobre o backup: trocar
        // mods debaixo de quem está jogando quebra a sessão de todo mundo.
        var server = Servidor();
        server.WorldInitializedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var repo = new FakeServers(server);
        var destino = Versao("2.0.0");

        var result = await NewChange(repo, new FakeStore(), destino, GameServerStatus.Running)
            .HandleAsync(server.Id, destino.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Servidor_sem_mundo_troca_sem_backup()
    {
        var server = Servidor();
        var repo = new FakeServers(server);
        var store = new FakeStore();
        var destino = Versao("2.0.0");

        var result = await NewChange(repo, store, destino, GameServerStatus.Stopped)
            .HandleAsync(server.Id, destino.Id, CancellationToken.None);

        Assert.True(result.Succeeded);

        // Nada a salvar: exigir backup aqui só faria o admin esperar à toa.
        Assert.False(store.Criou);
    }

    [Fact]
    public async Task Restaurar_exige_servidor_parado()
    {
        var server = Servidor();
        var backup = Snapshot(server, server.ModpackVersionId);
        var store = new FakeStore();

        var result = await NewRestore(server, backup, store, GameServerStatus.Running)
            .HandleAsync(backup.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(store.Restaurou);
    }

    [Fact]
    public async Task Restaurar_backup_de_outra_versao_pede_confirmacao()
    {
        // O mundo foi gerado por outros mods: pode não abrir, ou abrir perdendo
        // blocos. O admin decide, mas de olhos abertos.
        var server = Servidor();
        var backup = Snapshot(server, Guid.CreateVersion7());
        var store = new FakeStore();

        var useCase = NewRestore(server, backup, store, GameServerStatus.Stopped);

        var semConfirmar = await useCase.HandleAsync(backup.Id, CancellationToken.None);
        Assert.False(semConfirmar.Succeeded);
        Assert.False(store.Restaurou);

        var confirmado = await useCase.HandleAsync(backup.Id, CancellationToken.None, true);
        Assert.True(confirmado.Succeeded);
        Assert.True(store.Restaurou);
    }

    [Fact]
    public async Task Retencao_apaga_os_automaticos_que_passaram_do_limite()
    {
        // Sem poda, um servidor com troca de versão frequente acumula dezenas de
        // GB de .zip que ninguém percebe.
        var server = Servidor();
        var antigos = Enumerable.Range(0, 4)
            .Select(_ => Automatico(server))
            .ToArray();

        var repo = new FakeServers(server, existentes: antigos);
        var store = new FakeStore();

        await NewBackup(server, store, GameServerStatus.Stopped, repo, manter: 2)
            .HandleAsync(server.Id, null, CancellationToken.None, WorldBackupReason.BeforeVersionChange);

        // 4 antigos + o que acabou de ser criado = 5; guarda os 2 mais recentes,
        // os outros 3 saem do disco E do banco.
        Assert.Equal(3, repo.BackupsRemovidos.Count);
        Assert.Equal(3, store.Apagados.Count);
    }

    [Fact]
    public async Task Retencao_nunca_apaga_backup_manual()
    {
        // Snapshot manual foi um ato deliberado do admin — apagá-lo por política
        // seria o painel decidindo que o trabalho dele vale menos que disco.
        var server = Servidor();
        var manuais = Enumerable.Range(0, 4)
            .Select(_ => Snapshot(server, server.ModpackVersionId))
            .ToArray();

        var repo = new FakeServers(server, existentes: manuais);

        await NewBackup(server, new FakeStore(), GameServerStatus.Stopped, repo, manter: 1)
            .HandleAsync(server.Id, null, CancellationToken.None);

        Assert.Empty(repo.BackupsRemovidos);
    }

    [Fact]
    public async Task Retencao_zero_significa_ilimitado()
    {
        var server = Servidor();
        var antigos = Enumerable.Range(0, 5).Select(_ => Automatico(server)).ToArray();
        var repo = new FakeServers(server, existentes: antigos);

        await NewBackup(server, new FakeStore(), GameServerStatus.Stopped, repo, manter: 0)
            .HandleAsync(server.Id, null, CancellationToken.None, WorldBackupReason.BeforeVersionChange);

        Assert.Empty(repo.BackupsRemovidos);
    }

    [Fact]
    public async Task Apagar_backup_remove_o_arquivo_e_o_registro()
    {
        var server = Servidor();
        var backup = Snapshot(server, server.ModpackVersionId);
        var repo = new FakeServers(server, backup);
        var store = new FakeStore();

        var result = await new DeleteWorldBackup(repo, store).HandleAsync(backup.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(store.Apagou);
        Assert.True(repo.BackupRemovido);
    }

    // ---- Fixtures ----

    private static readonly Guid ModpackId = Guid.CreateVersion7();
    private static readonly Guid VersaoAtualId = Guid.CreateVersion7();

    private static GameServer Servidor() => new()
    {
        Name = "Servidor",
        ModpackId = ModpackId,
        ModpackVersionId = VersaoAtualId,
        ConnectAddress = "jogo:25565",
        RconSecret = "segredo"
    };

    private static WorldBackup Automatico(GameServer server) => new()
    {
        GameServerId = server.Id,
        FileName = $"{Guid.CreateVersion7()}.zip",
        SizeBytes = 1024,
        Reason = WorldBackupReason.BeforeVersionChange,
        ModpackVersionId = server.ModpackVersionId
    };

    private static WorldBackup Snapshot(GameServer server, Guid versionId) => new()
    {
        GameServerId = server.Id,
        FileName = "20260809-120000.zip",
        SizeBytes = 1024,
        Reason = WorldBackupReason.Manual,
        ModpackVersionId = versionId,
        ModpackVersionLabel = "1.0.0"
    };

    private static ModpackVersion Versao(string numero)
    {
        var version = new ModpackVersion
        {
            ModpackId = ModpackId, Version = numero, LoaderVersion = "21.1.100"
        };

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
        return version;
    }

    private static CreateWorldBackup NewBackup(
        GameServer server, FakeStore store, GameServerStatus status,
        FakeServers? repo = null, int manter = 0, FakeRcon? rcon = null) =>
        new(repo ?? new FakeServers(server), new FakeOrchestrator(status), rcon ?? new FakeRcon(), store,
            new FakeModpacks(VersaoComNumero("1.0.0", VersaoAtualId)),
            new FakeSettings(manter), new FakeJobProgress());

    private static ChangeServerVersion NewChange(
        FakeServers repo, FakeStore store, ModpackVersion destino, GameServerStatus status)
    {
        var orchestrator = new FakeOrchestrator(status);
        var modpacks = new FakeModpacks(destino);

        var backup = new CreateWorldBackup(
            repo, orchestrator, new FakeRcon(), store, modpacks,
            new FakeSettings(), new FakeJobProgress());
        return new ChangeServerVersion(repo, modpacks, orchestrator, backup);
    }

    private static RestoreWorldBackup NewRestore(
        GameServer server, WorldBackup backup, FakeStore store, GameServerStatus status) =>
        new(new FakeServers(server, backup), new FakeOrchestrator(status), store, new FakeJobProgress());

    private static ModpackVersion VersaoComNumero(string numero, Guid id)
    {
        var version = Versao(numero);

        // O caso de uso busca a versão fixada pelo Id do servidor; o fake devolve
        // esta, e o rótulo é o que vai para o registro do backup.
        typeof(TCMine.Server.Domain.Common.Entity)
            .GetProperty(nameof(TCMine.Server.Domain.Common.Entity.Id))!
            .SetValue(version, id);

        return version;
    }

    // ---- Fakes ----

    private sealed class FakeRcon : IRconClient
    {
        public List<string> Comandos { get; } = [];
        public bool FalhaNoSaveOn { get; init; }
        public bool Indisponivel { get; init; }

        public Task<string> ExecuteAsync(Guid gameServerId, string rawCommand, CancellationToken ct)
        {
            if (Indisponivel)
                throw new RconUnavailableException("container não responde");

            if (FalhaNoSaveOn && rawCommand is "save-on")
                throw new RconUnavailableException("container morreu no meio");

            Comandos.Add(rawCommand);
            return Task.FromResult("ok");
        }
    }

    private sealed class FakeSettings(int keepCount = 0) : ISettingsRepository
    {
        public Task<InstallationSettings> GetAsync(CancellationToken ct) =>
            Task.FromResult(new InstallationSettings { WorldBackupKeepCount = keepCount });

        public Task SaveAsync(InstallationSettings settings, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> GetSmtpPasswordAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed class FakeStore : IWorldBackupStore
    {
        public List<string> Apagados { get; } = [];
        public bool Apagou => Apagados.Count > 0;
        public bool Criou { get; private set; }
        public bool Explode { get; init; }
        public bool Restaurou { get; private set; }
        public bool SemMundo { get; init; }

        public Task<StoredWorldBackup?> CreateAsync(
            Guid gameServerId, Action<int, int>? onProgress, CancellationToken ct)
        {
            if (Explode)
                throw new IOException("disco cheio");

            if (SemMundo)
                return Task.FromResult<StoredWorldBackup?>(null);

            Criou = true;
            return Task.FromResult<StoredWorldBackup?>(new StoredWorldBackup("20260809-120000.zip", 2048));
        }

        public Task<bool> RestoreAsync(
            Guid gameServerId, string fileName, Action<int, int>? onProgress, CancellationToken ct)
        {
            Restaurou = true;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid gameServerId, string fileName, CancellationToken ct)
        {
            Apagados.Add(fileName);
            return Task.FromResult(true);
        }

        public Task<Stream?> OpenAsync(Guid gameServerId, string fileName, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeOrchestrator(GameServerStatus status) : IServerOrchestrator
    {
        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult(status);

        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task StartAsync(Guid gameServerId, CancellationToken ct) => throw new NotImplementedException();

        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<string> StreamLogsAsync(Guid gameServerId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeModpacks(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);
    }

    private sealed class FakeServers(
        GameServer server, WorldBackup? backup = null, params WorldBackup[] existentes)
        : FakeServerRepositoryBase
    {
        private readonly List<WorldBackup> _backups = [.. existentes];

        public WorldBackup? Adicionado { get; private set; }
        public List<Guid> BackupsRemovidos { get; } = [];
        public bool BackupRemovido => BackupsRemovidos.Count > 0;

        public override Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WorldBackup>>([.. _backups.OrderByDescending(b => b.Id)]);

        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<GameServer?>(server);

        public override Task UpdateAsync(GameServer s, CancellationToken ct) => Task.CompletedTask;

        public override Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct) =>
            Task.FromResult(backup);

        public override Task AddBackupAsync(WorldBackup b, CancellationToken ct)
        {
            Adicionado = b;
            _backups.Add(b);
            return Task.CompletedTask;
        }

        public override Task RemoveBackupAsync(Guid backupId, CancellationToken ct)
        {
            BackupsRemovidos.Add(backupId);
            _backups.RemoveAll(b => b.Id == backupId);
            return Task.CompletedTask;
        }
    }
}
