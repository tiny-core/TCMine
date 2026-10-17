using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>
///     Aba "Jogadores": canais de cada jogador, totais e quem está com o lease.
///     Daqui o dono abre os itens de um canal, descongela, libera lease preso e
///     pula para o histórico do jogador na Auditoria. A lista vem por página.
/// </summary>
public partial class CloudPlayersPanel : ComponentBase
{
    private bool _isBusy;

    /// <summary>Próxima página a pedir (base zero).</summary>
    private int _nextPage;

    private List<CloudPlayerView>? _players;
    private string? _search;
    private int _total;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] public IReadOnlyDictionary<Guid, string> ServerNames { get; set; } = new Dictionary<Guid, string>();

    /// <summary>"Ver histórico": a página da nuvem abre a Auditoria filtrada por este UUID.</summary>
    [Parameter] public EventCallback<string> ShowHistory { get; set; }

    [Inject] private ListCloudPlayers ListUseCase { get; set; } = default!;
    [Inject] private ForceReleaseCloudLease ForceReleaseUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    // Busca nova (ou recarga depois de uma ação) volta para a primeira página:
    // as páginas já abertas podiam ter mudado de ordem ou de conteúdo.
    private Task LoadAsync()
    {
        _nextPage = 0;
        return LoadPageAsync(replace: true);
    }

    private Task LoadMoreAsync() => LoadPageAsync(replace: false);

    private async Task LoadPageAsync(bool replace)
    {
        _isBusy = true;
        try
        {
            var result = await ListUseCase.HandleAsync(VaultId, _search, _nextPage, CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            if (replace || _players is null)
                _players = [.. result.Value!.Items];
            else
                _players.AddRange(result.Value!.Items);
            _total = result.Value.TotalCount;
            _nextPage++;
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
