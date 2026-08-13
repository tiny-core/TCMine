using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Servers;

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
        foreach (var server in servers)
        {
            var real = await orchestrator.GetStatusAsync(server.Id, ct);
            if (real == server.Status)
                continue;

            server.Status = real;
            await repository.UpdateAsync(server, ct);
        }
    }

    public Task<bool> StartAsync(Guid serverId, CancellationToken ct) =>
        RunAsync(() => startUseCase.HandleAsync(serverId, ct));

    public Task<bool> StopAsync(Guid serverId, CancellationToken ct) =>
        RunAsync(() => stopUseCase.HandleAsync(serverId, ct));

    public Task<bool> DeleteAsync(Guid serverId, CancellationToken ct) =>
        RunAsync(() => deleteUseCase.HandleAsync(serverId, ct), "Servidor removido.");

    private async Task<bool> RunAsync(
        Func<Task<TCMine.Server.Application.Common.Result>> action, string? successMessage = null)
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
