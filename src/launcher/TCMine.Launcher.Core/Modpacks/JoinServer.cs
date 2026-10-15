using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Abre o jogo já entrando num servidor, pela instância DO SERVIDOR.
///     A versão é pinada NO SERVIDOR, não no modpack (CLAUDE.md §5), e a
///     instância de servidor existe para segui-la: para cima ou para baixo, sem
///     backup. O mundo de quem joga num servidor mora no servidor; a pasta local
///     só leva mods e configs, e descer de versão nela não parte nada — o
///     contrário da instância pessoal (§7.0), que só anda para a frente e nunca
///     entra em servidor. Mundo próprio em <c>saves/</c> é exceção (alguém abriu
///     um mundo solo nela): segue, com aviso.
///     Uma instância por servidor, achada pelo id dele no manifesto. Não há:
///     instala uma nova. O armazém de conteúdo partilhado faz o segundo download
///     do mesmo pack ser só materialização.
/// </summary>
public sealed class JoinServer(
    IServerConnection connection,
    IInstanceStore instances,
    IInstanceInstaller installer,
    IWorldBackup worlds,
    LaunchGame launch,
    IEndpointProbe probe,
    IGameTunnel tunnel)
{
    /// <summary>
    ///     <paramref name="modpack" /> é o do servidor, vindo do catálogo que a
    ///     tela já tem: é o que cria a instância quando ainda não há nenhuma, e
    ///     pedi-lo ao servidor de novo seria uma ida à rede por dado em memória.
    /// </summary>
    public async Task<JoinServerResult> HandleAsync(
        ModpackDto modpack,
        GameServerDto server,
        LauncherConfig config,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        if (server.AccessState is not ServerAccessState.Granted)
            return JoinServerResult.Failed("Você ainda não tem acesso a este servidor.");

        if (ServerAddress.Parse(server.ConnectAddress) is null
            && ServerAddress.Parse(server.LocalAddress) is null
            && string.IsNullOrWhiteSpace(server.TunnelAddress))
        {
            return JoinServerResult.Failed(
                $"{server.Name} ainda não tem endereço. Peça ao administrador para conferir a aba Rede "
                + "das configurações do painel.");
        }

        if (modpack.Id != server.ModpackId)
            return JoinServerResult.Failed($"{server.Name} roda outro modpack.");

        var key = await instances.FindServerInstanceAsync(server.Id, ct);
        var manifest = key is { } found ? await instances.ReadManifestAsync(found, ct) : null;

        // O admin trocou o pack do servidor. A pasta antiga não serve de base
        // (outro Minecraft, outro loader), mas pode ter um mundo solo: vira
        // pessoal em vez de ser apagada, e o servidor ganha uma instância nova.
        if (key is { } previous && manifest is not null && manifest.ModpackId != server.ModpackId)
        {
            await instances.WriteManifestAsync(previous, manifest with { ServerId = null, ServerName = null }, ct);
            key = null;
            manifest = null;
        }

        string? warning = null;

        if (manifest is null || manifest.ModpackVersionId != server.ModpackVersionId)
        {
            progress?.Report(new GameLaunchProgress("Conferindo a versão do servidor"));

            ModpackVersionDto target;

            try
            {
                target = await connection.GetModpackVersionAsync(server.ModpackVersionId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return JoinServerResult.Failed($"Não foi possível consultar a versão de {server.Name}: {ex.Message}");
            }

            if (key is { } existing && manifest is not null
                && Decide(manifest.Version, target.Version) is JoinStep.Downgrade
                && worlds.HasWorld(existing))
            {
                warning = $"{server.Name} voltou para a v{target.Version}. O mundo solo guardado nesta "
                          + "instância (pasta saves) pode não abrir nesta versão — o do servidor não é afetado.";
            }

            var verb = manifest is null ? "Instalando" : "Atualizando para";

            var install = await installer.HandleAsync(
                config.ServerUrl,
                modpack,
                target.Id,
                key,
                progress is null
                    ? null
                    : new SyncProgress<InstallProgress>(p =>
                    {
                        if (p.Label is { } label)
                            progress.Report(new GameLaunchProgress($"{verb} v{target.Version}: {label}", p.Fraction));
                    }),
                ct);

            if (!install.Succeeded)
                return JoinServerResult.Failed(install.Error);

            key = install.Key!.Value;
            manifest = install.Instance!;
        }

        // A marca de servidor vai logo após a instalação, que grava o manifesto
        // sem ela numa instância nova. O nome acompanha um servidor renomeado no
        // painel: é o que a tela de instâncias mostra.
        var instanceKey = key!.Value;

        if (manifest.ServerId != server.Id || manifest.ServerName != server.Name)
        {
            manifest = manifest with { ServerId = server.Id, ServerName = server.Name };
            await instances.WriteManifestAsync(instanceKey, manifest, ct);
        }

        // Tamanho zero: somar a pasta é varrer um mundo inteiro, e quem abre o
        // jogo não o mostra. A tela relê a instância quando precisa dele.
        var instance = new InstalledInstance(instanceKey, manifest, 0, instances.PathFor(instanceKey));

        // Escolhido só agora, depois de uma eventual atualização: ela pode
        // levar minutos, e o jogador pode ter mudado de rede nesse meio-tempo.
        progress?.Report(new GameLaunchProgress("Procurando o caminho até o servidor"));
        var route = await ServerRoute.ChooseAsync(server, probe, ct);
        var address = route.Address;
        IGameTunnelSession? session = null;

        if (route.TunnelHost is { } tunnelHost)
        {
            progress?.Report(new GameLaunchProgress("Abrindo o túnel até o servidor"));
            session = await tunnel.OpenAsync(tunnelHost, ct);

            // Túnel fora do ar: o endereço publicado ainda pode funcionar (a
            // sonda pode ter errado), e se não funcionar, o jogo diz porquê.
            address = session?.LocalAddress ?? ServerAddress.Parse(server.ConnectAddress);
        }

        GameLaunchResult result;
        try
        {
            result = await launch.HandleAsync(instance, config, address, progress, ct);
        }
        catch
        {
            if (session is not null)
                await session.DisposeAsync();
            throw;
        }

        if (session is not null)
        {
            if (result.Process is { } process)
                result = result with { Process = new TunneledGameProcess(process, session) };
            else
                await session.DisposeAsync(); // o jogo nem abriu: nada a carregar
        }

        return new JoinServerResult(result, instance, warning);
    }

    /// <summary>
    ///     Para que lado a instância do servidor anda. Os dois seguem; descer só
    ///     decide se há aviso. Sem como comparar os números, conta como descer:
    ///     na dúvida, avisar custa uma linha, e calar pode custar um mundo.
    /// </summary>
    public static JoinStep Decide(string installedVersion, string serverVersion) =>
        ModpackVersionOrder.Compare(serverVersion, installedVersion) is >= 0
            ? JoinStep.Update
            : JoinStep.Downgrade;

    /// <summary>
    ///     <see cref="IProgress{T}" /> que repassa na hora. O <c>Progress&lt;T&gt;</c>
    ///     postaria no contexto de sincronização, e o texto da atualização chegaria
    ///     à tela depois do da fase seguinte.
    /// </summary>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

public enum JoinStep
{
    Update,
    Downgrade
}

/// <summary>
///     O resultado da abertura, a instância do servidor como ficou (a tela
///     passa a mostrá-la) e um aviso que não impediu a entrada.
/// </summary>
public sealed record JoinServerResult(
    GameLaunchResult Launch,
    InstalledInstance? Instance = null,
    string? Warning = null)
{
    public static JoinServerResult Failed(string? message) =>
        new(GameLaunchResult.Failed(message ?? "Falha desconhecida."));
}
