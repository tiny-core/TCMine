using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Abre o jogo já entrando num servidor, pela instância DO SERVIDOR.
///     Achar, criar e alinhar essa instância é do <see cref="PrepareServerInstance" />
///     (a mesma regra do "Instalar" da aba Servidores); daqui para a frente é
///     escolher o caminho, abrir o jogo e deixar a instância como a ativa.
/// </summary>
public sealed class JoinServer(
    PrepareServerInstance prepare,
    IActiveInstanceStore active,
    LaunchGame launch,
    IEndpointProbe probe,
    IGameTunnel tunnel)
{
    /// <summary>
    ///     <paramref name="modpack" /> é o do servidor (ver
    ///     <see cref="PrepareServerInstance.HandleAsync" />).
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

        // Antes de instalar: baixar centenas de megabytes para então descobrir
        // que não há por onde entrar seria fazer esperar à toa.
        if (ServerAddress.Parse(server.ConnectAddress) is null
            && ServerAddress.Parse(server.LocalAddress) is null
            && string.IsNullOrWhiteSpace(server.TunnelAddress))
        {
            return JoinServerResult.Failed(
                $"{server.Name} ainda não tem endereço. Peça ao administrador para conferir a aba Rede "
                + "das configurações do painel.");
        }

        var prepared = await prepare.HandleAsync(
            config.ServerUrl,
            modpack,
            server,
            progress is null
                ? null
                : new SyncProgress<InstallProgress>(p =>
                {
                    if (p.Label is { } label)
                        progress.Report(new GameLaunchProgress($"Preparando a instância de {server.Name}: {label}",
                            p.Fraction));
                }),
            ct);

        if (prepared.Instance is not { } instance)
            return JoinServerResult.Failed(prepared.Error);

        var warning = prepared.Warning;

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
            // O embrulho vai PARA DENTRO do LaunchGame, para a sessão ler o
            // processo embrulhado: é ao fim dessa leitura que o túnel fecha.
            result = await launch.HandleAsync(
                instance, config, address,
                session is null ? null : process => new TunneledGameProcess(process, session),
                progress, ct);
        }
        catch
        {
            if (session is not null)
                await session.DisposeAsync();
            throw;
        }

        // O jogo nem abriu: nada a carregar.
        if (session is not null && result.Process is null)
            await session.DisposeAsync();

        // A ativa passa a ser a do servidor: é nela que o jogador acabou de
        // entrar, e o "Jogar" seguinte tem de abrir a mesma pasta. Só com o jogo
        // aberto — uma entrada que falhou não muda o que a tela inicial mostra.
        if (result.Started)
            await active.WriteAsync(instance.Key, ct);

        return new JoinServerResult(result, instance, warning);
    }

    /// <summary>
    ///     Para que lado a instância do servidor anda (ver <see cref="PrepareServerInstance" />). Os dois seguem; descer só
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
