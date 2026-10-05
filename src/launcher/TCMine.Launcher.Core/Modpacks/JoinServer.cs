using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Abre o jogo já entrando num servidor.
///     A versão é pinada NO SERVIDOR, não no modpack (CLAUDE.md §5), então a
///     instância do jogador pode estar noutra. Entrar com mods diferentes dos do
///     servidor é recusado pelo próprio jogo — por isso este caso de uso alinha a
///     instância ANTES de abrir: atualiza quando o servidor está à frente, com o
///     mesmo backup de sempre, e recusa quando está atrás.
/// </summary>
public sealed class JoinServer(IServerConnection connection, UpdateInstance updater, LaunchGame launch)
{
    public async Task<JoinServerResult> HandleAsync(
        InstalledInstance instance,
        GameServerDto server,
        LauncherConfig config,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken ct)
    {
        if (server.AccessState is not ServerAccessState.Granted
            || ServerAddress.Parse(server.ConnectAddress) is not { } address)
            return JoinServerResult.Failed("Você ainda não tem acesso a este servidor.");

        if (server.ModpackId != instance.Manifest.ModpackId)
            return JoinServerResult.Failed($"{server.Name} roda outro modpack.");

        var updated = false;

        if (server.ModpackVersionId != instance.Manifest.ModpackVersionId)
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

            if (Decide(instance.Manifest.Version, target.Version) is JoinStep.Refuse)
            {
                return JoinServerResult.Failed(
                    $"{server.Name} está na v{target.Version}, e esta instância na v{instance.Manifest.Version}. "
                    + "Voltar a uma versão anterior por cima dela quebraria o seu mundo — "
                    + "espere o servidor ser atualizado.");
            }

            var install = await updater.HandleAsync(
                config.ServerUrl,
                instance.Manifest.ToModpack(),
                target.Id,
                instance,
                backupWorld: true,
                progress is null
                    ? null
                    : new SyncProgress<InstallProgress>(p =>
                    {
                        if (p.Label is { } label)
                            progress.Report(new GameLaunchProgress($"Atualizando para v{target.Version}: {label}", p.Fraction));
                    }),
                ct);

            if (!install.Succeeded)
                return JoinServerResult.Failed(install.Error);

            // Mesma chave, mesma pasta: só o manifesto mudou.
            instance = instance with { Manifest = install.Instance! };
            updated = true;
        }

        var result = await launch.HandleAsync(instance, config, address, progress, ct);

        return new JoinServerResult(result, updated ? instance : null);
    }

    /// <summary>
    ///     Atualizar ou recusar, quando a instância e o servidor discordam.
    ///     Recusa quando o servidor está ATRÁS — uma instância só anda para a
    ///     frente (§7.0) — e também quando não dá para comparar: na dúvida, a
    ///     opção que não arrisca o mundo.
    /// </summary>
    public static JoinStep Decide(string installedVersion, string serverVersion) =>
        ModpackVersionOrder.Compare(serverVersion, installedVersion) is >= 0
            ? JoinStep.Update
            : JoinStep.Refuse;

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
    Refuse
}

/// <summary>
///     O resultado da abertura, e a instância como ficou quando ela foi
///     atualizada no caminho — a tela de jogar mostra a versão, e sem isto
///     continuaria a mostrar a antiga.
/// </summary>
public sealed record JoinServerResult(GameLaunchResult Launch, InstalledInstance? Updated = null)
{
    public static JoinServerResult Failed(string? message) => new(GameLaunchResult.Failed(message ?? "Falha desconhecida."));
}
