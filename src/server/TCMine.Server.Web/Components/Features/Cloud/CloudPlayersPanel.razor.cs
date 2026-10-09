using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>
///     Aba "Jogadores": canais de cada jogador, totais e quem está com o lease.
///     Daqui o dono abre os itens de um canal, descongela e libera lease preso.
/// </summary>
public partial class CloudPlayersPanel : ComponentBase
{
    private const int ListLimit = ListCloudPlayers.Limit;
    private bool _isBusy;

    private List<CloudPlayerView>? _players;
    private string? _search;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] public IReadOnlyDictionary<Guid, string> ServerNames { get; set; } = new Dictionary<Guid, string>();

    [Inject] private ListCloudPlayers ListUseCase { get; set; } = default!;
    [Inject] private ForceReleaseCloudLease ForceReleaseUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _isBusy = true;
        try
        {
            var result = await ListUseCase.HandleAsync(VaultId, _search, CancellationToken.None);
            if (result.Succeeded)
                _players = [.. result.Value!];
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task OpenChannelAsync(CloudPlayerView player, CloudChannelView channel)
    {
        var parameters = new DialogParameters<CloudChannelDialog>
        {
            { x => x.VaultId, VaultId },
            { x => x.Channel, channel },
            { x => x.PlayerName, player.DisplayName ?? player.PlayerUuid }
        };
        var dialog = await DialogService.ShowAsync<CloudChannelDialog>(channel.Name, parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task ForceReleaseAsync(CloudPlayerView player)
    {
        var confirm = await DialogService.ShowMessageBoxAsync("Liberar lease à força",
            "Use só se o servidor que está com os canais morreu de vez. Se ele voltar, os lotes que ainda tinha " +
            "no diário vão para a quarentena em vez de serem aplicados.",
            "Liberar", cancelText: "Cancelar");
        if (confirm != true)
            return;

        var result = await ForceReleaseUseCase.HandleAsync(VaultId, player.PlayerUuid, CancellationToken.None);
        Snackbar.Add(result.Succeeded ? "Lease liberado." : result.Error!,
            result.Succeeded ? Severity.Success : Severity.Error);
        await LoadAsync();
    }
}
