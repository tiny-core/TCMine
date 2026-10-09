using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Itens de um canal (só leitura) e, se congelado, o botão de descongelar.</summary>
public partial class CloudChannelDialog : DialogComponentBase
{
    private List<CloudBalanceView>? _items;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] [EditorRequired] public CloudChannelView Channel { get; set; } = default!;
    [Parameter] public string PlayerName { get; set; } = "";

    [Inject] private GetCloudChannelBalances BalancesUseCase { get; set; } = default!;
    [Inject] private UnfreezeCloudChannel UnfreezeUseCase { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var result = await BalancesUseCase.HandleAsync(VaultId, Channel.Id, CancellationToken.None);
        if (result.Succeeded)
            _items = [.. result.Value!];
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private Task UnfreezeAsync() =>
        SubmitAsync(() => UnfreezeUseCase.HandleAsync(VaultId, Channel.Id, CancellationToken.None),
            "Canal descongelado.");
}
