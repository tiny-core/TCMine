using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     Atualizar uma instância sem arriscar o mundo.
///     O mundo é a única coisa numa instância que não se baixa de novo, e a regra
///     que o protege não é "fazer backup" — é a ORDEM: copiar antes, e desistir
///     da atualização se a cópia falhar.
/// </summary>
public class UpdateInstanceTests
{
    // ---------- apoio ----------

    private static readonly Uri Servidor = new("https://servidor.exemplo/");

    private static readonly ModpackDto Pack = new()
    {
        Id = Guid.CreateVersion7(),
        Slug = "pack",
        Name = "Pack",
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task O_backup_acontece_antes_de_a_instalacao_comecar()
    {
        // Começar a reescrever mods para só então copiar deixaria o jogador sem
        // os dois caminhos: nem a versão antiga, nem a garantia de poder voltar.
        var ordem = new List<string>();
        var backup = new BackupFalso(true, ordem);
        var scenario = Cenario(backup, ordem);

        await scenario.HandleAsync(
            Servidor, Pack, Guid.CreateVersion7(), Instalada(), true, null, Ct);

        ordem.ShouldBe(["backup", "instalação"]);
    }

    [Fact]
    public async Task Backup_que_falha_cancela_a_atualizacao()
    {
        // É isto que a torna reversível. Instalar por cima de uma cópia que não
        // existe é o caso em que o jogador perde tudo sem ninguém saber.
        var ordem = new List<string>();
        var backup = new BackupFalso(true, ordem) { Erro = new IOException("disco cheio") };
        var scenario = Cenario(backup, ordem);

        var result = await scenario.HandleAsync(
            Servidor, Pack, Guid.CreateVersion7(), Instalada(), true, null, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("disco cheio");
        ordem.ShouldNotContain("instalação");
    }

    [Fact]
    public async Task Instancia_sem_mundo_nao_gera_copia()
    {
        // Propor backup de uma instalação que nunca foi jogada ensina o jogador a
        // ignorar o aviso.
        var ordem = new List<string>();
        var backup = new BackupFalso(false, ordem);
        var scenario = Cenario(backup, ordem);

        await scenario.HandleAsync(
            Servidor, Pack, Guid.CreateVersion7(), Instalada(), true, null, Ct);

        ordem.ShouldBe(["instalação"]);
    }

    [Fact]
    public async Task Sem_pedir_backup_nem_se_pergunta_pelo_mundo()
    {
        var ordem = new List<string>();
        var backup = new BackupFalso(true, ordem);
        var scenario = Cenario(backup, ordem);

        await scenario.HandleAsync(
            Servidor, Pack, Guid.CreateVersion7(), Instalada(), false, null, Ct);

        ordem.ShouldBe(["instalação"]);
    }

    [Fact]
    public async Task A_atualizacao_vai_para_a_MESMA_instancia()
    {
        // O ponto inteiro desta fatia. Com alvo nulo o instalador criaria uma
        // instalação ao lado e o mundo ficaria para trás na antiga.
        var ordem = new List<string>();
        var installer = new InstaladorFalso(ordem);
        var instance = Instalada();

        await new UpdateInstance(installer, new BackupFalso(false, ordem)).HandleAsync(
            Servidor, Pack, Guid.CreateVersion7(), instance, true, null, Ct);

        installer.Alvo.ShouldBe(instance.Key);
    }

    private static UpdateInstance Cenario(BackupFalso backup, List<string> ordem) =>
        new(new InstaladorFalso(ordem), backup);

    private static InstalledInstance Instalada() =>
        new(InstanceKey.New(),
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = Pack.Id,
                ModpackVersionId = Guid.CreateVersion7(),
                ModpackName = "Pack",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>(),
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge
            },
            0,
            "/instancias/pack");

    private sealed class BackupFalso(bool temMundo, List<string> ordem) : IWorldBackup
    {
        public Exception? Erro { get; init; }

        public bool HasWorld(InstanceKey key) => temMundo;

        public Task<string> CreateAsync(InstanceKey key, CancellationToken ct)
        {
            ordem.Add("backup");

            return Erro is not null
                ? Task.FromException<string>(Erro)
                : Task.FromResult("/backups/x.zip");
        }
    }

    /// <summary>
    ///     O que se verifica aqui é a orquestração — ordem e alvo —, não o que a
    ///     instalação faz por dentro.
    /// </summary>
    private sealed class InstaladorFalso(List<string> ordem) : IInstanceInstaller
    {
        public InstanceKey? Alvo { get; private set; }

        public Task<InstallResult> HandleAsync(
            Uri serverUrl,
            ModpackDto modpack,
            Guid versionId,
            InstanceKey? target,
            IProgress<InstallProgress>? progress,
            CancellationToken ct)
        {
            ordem.Add("instalação");
            Alvo = target;

            return Task.FromResult(InstallResult.Success(target ?? InstanceKey.New(), Instalada().Manifest));
        }
    }
}
