using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Options;

/// <summary>
///     As teclas do jogador, por modpack, entre a conta e o options.txt de cada
///     instância — pessoal ou de servidor, neste PC ou noutro.
///     Baixa antes de abrir o jogo e envia quando ele fecha. A regra de quem
///     vence é uma só: o que mudou por último. "Mudou" é contra a última
///     sincronização desta instância (<see cref="OptionsSyncState" />), e não
///     contra o servidor: um jogo que fechou sem rede deixa o arquivo à frente
///     da conta, e a abertura seguinte tem de o ENVIAR, e não de o atropelar
///     com o que a conta tinha.
///     Nunca impede de jogar: sem rede ou sem servidor, o jogo abre com o
///     arquivo que está no disco.
///     Singleton (ver o registo): quem envia ao fechar é um coordenador que
///     vive a aplicação inteira, e tudo de que isto depende também vive.
/// </summary>
public sealed partial class SyncPlayerOptions(
    IServerConnection connection,
    IPlayerOptionsStore store,
    ILauncherPreferences preferences,
    ILogger<SyncPlayerOptions>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<SyncPlayerOptions>.Instance;

    /// <summary>Antes de abrir o jogo: traz o que a conta tem de mais novo, ou envia o que ficou para trás.</summary>
    public async Task BeforeLaunchAsync(InstalledInstance instance, CancellationToken ct)
    {
        try
        {
            var all = (await preferences.ReadAsync(ct)).SyncAllOptions;
            var modpackId = instance.Manifest.ModpackId;

            var remote = await connection.GetModpackSettingsAsync(modpackId, ct);
            var local = await store.ReadAsync(instance.Key, ct);
            var state = await store.ReadStateAsync(instance.Key, ct);

            var payload = local is null ? null : OptionsFile.Extract(local.Text, all);
            var localChanged = payload is not null && payload.Fingerprint != state?.Fingerprint;
            var remoteChanged = remote is not null && remote.UpdatedAt != state?.RemoteSavedAt;

            // Os dois mudaram (este PC jogou sem rede, e outro enviou depois):
            // vence o mais recente, pela hora do arquivo contra a da conta.
            if (localChanged && (!remoteChanged || local!.LastWriteAt > remote!.UpdatedAt))
            {
                await PushAsync(instance, payload!, ct);
                return;
            }

            if (!remoteChanged)
                return;

            var merged = OptionsFile.Merge(local?.Text, remote!.KeyBindings, all ? remote.Options : null);
            await store.WriteAsync(instance.Key, merged, ct);

            var applied = OptionsFile.Extract(merged, all);
            await store.WriteStateAsync(instance.Key, new OptionsSyncState(applied.Fingerprint, remote.UpdatedAt), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSyncFailed(ex, instance.Manifest.ModpackName);
        }
    }

    /// <summary>Depois de o jogo fechar: envia, se o jogador mudou alguma coisa.</summary>
    public async Task AfterExitAsync(InstalledInstance instance, CancellationToken ct)
    {
        try
        {
            if (await store.ReadAsync(instance.Key, ct) is not { } local)
                return;

            var all = (await preferences.ReadAsync(ct)).SyncAllOptions;
            var payload = OptionsFile.Extract(local.Text, all);
            var state = await store.ReadStateAsync(instance.Key, ct);

            if (payload.Fingerprint != state?.Fingerprint)
                await PushAsync(instance, payload, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem rede: fica para a próxima abertura, que vê o arquivo à frente
            // da última sincronização e envia antes de baixar.
            LogSyncFailed(ex, instance.Manifest.ModpackName);
        }
    }

    private async Task PushAsync(InstalledInstance instance, OptionsPayload payload, CancellationToken ct)
    {
        var modpackId = instance.Manifest.ModpackId;

        await connection.SaveModpackSettingsAsync(modpackId, payload.KeyBindings, payload.Rest, ct);

        // A hora da gravação é a do SERVIDOR, e é ela que a comparação seguinte
        // usa. Guardar a deste PC faria dois relógios discordarem.
        var saved = await connection.GetModpackSettingsAsync(modpackId, ct);

        await store.WriteStateAsync(
            instance.Key,
            new OptionsSyncState(payload.Fingerprint, saved?.UpdatedAt ?? DateTimeOffset.MinValue),
            ct);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "As teclas de {Modpack} não foram sincronizadas; o jogo segue com as do disco.")]
    private partial void LogSyncFailed(Exception ex, string modpack);
}
