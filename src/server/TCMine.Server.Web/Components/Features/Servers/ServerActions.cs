using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Features.Servers;

/// <summary>
///     Ações de servidor compartilhadas entre a aba do modpack e a lista global.
///     Existe para as duas telas não repetirem ligar/parar/apagar e, sobretudo, a
///     reconciliação de status: a coluna no banco é cache, o container é a
///     verdade. Duas cópias dessa regra divergiriam na primeira correção.
/// </summary>
public sealed class ServerActions(
    IServerRepository repository,
    IServerOrchestrator orchestrator,
    StartGameServer startUseCase,
    StopGameServer stopUseCase,
    DeleteGameServer deleteUseCase,
    IActivityLogRepository activity,
    ISnackbar snackbar)
{
    /// <summary>
    ///     Alinha o status gravado com o que o Docker diz.
    ///     Um container `unless-stopped` sobrevive a reinícios do TCMine, e o
    ///     admin pode ter parado tudo por fora — sem isto a tela mostraria
    ///     "rodando" para um servidor morto.
    /// </summary>
    public async Task SyncStatusesAsync(IEnumerable<GameServer> servers, CancellationToken ct)
    {
        // Os eventos do Docker já mantêm a coluna em dia (ServerStatusReconciler);
        // isto continua como rede de segurança para quando eles estão em baixo.
        foreach (var server in servers)
        {
            var real = await orchestrator.GetStatusAsync(server.Id, ct);
            await ServerStatusTransitions.ApplyAsync(server, real, repository, activity, ct);
        }
    }

    /// <summary>
    ///     Ligar e parar não são instantâneos — o primeiro start materializa a
    ///     instância e pode puxar imagem, e o stop espera o mundo salvar (até um
    ///     minuto). Ambos reportam ao acompanhamento global, que a barra do topo
    ///     mostra em qualquer página.
    /// </summary>
    public Task<bool> StartAsync(Guid serverId, CancellationToken ct) =>
        RunAsync(() => startUseCase.HandleAsync(serverId, ct, Guid.CreateVersion7()));

    public Task<bool> StopAsync(Guid serverId, CancellationToken ct) =>
        RunAsync(() => stopUseCase.HandleAsync(serverId, ct, Guid.CreateVersion7()));

    /// <summary>
    ///     Parar o container espera o mundo salvar e apagar a instância varre
    ///     milhares de arquivos — leva o tempo que leva, e por isso reporta.
    /// </summary>
    public Task<bool> DeleteAsync(Guid serverId, CancellationToken ct) =>
        RunAsync(() => deleteUseCase.HandleAsync(serverId, ct, Guid.CreateVersion7()), "Servidor removido.");

    private async Task<bool> RunAsync(
        Func<Task<Result>> action, string? successMessage = null)
    {
        var result = await action();

        if (!result.Succeeded)
        {
            snackbar.Add(result.Error!, Severity.Error);
            return false;
        }

        if (successMessage is not null)
            snackbar.Add(successMessage, Severity.Success);

        return true;
    }

    public static Color StatusColor(GameServerStatus status) => status switch
    {
        GameServerStatus.Running => Color.Success,
        GameServerStatus.Starting or GameServerStatus.Stopping => Color.Info,
        GameServerStatus.Crashed => Color.Error,
        GameServerStatus.Updating => Color.Warning,
        _ => Color.Default
    };
}
