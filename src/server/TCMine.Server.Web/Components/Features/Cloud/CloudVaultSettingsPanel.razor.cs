using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>
///     Aba "Configurações": nome, liga/desliga, modo da política e limites. Os
///     servidores de jogo recebem os valores novos no próximo hello (reinício);
///     o TCMine já os aplica na hora ao conferir os lotes.
/// </summary>
public partial class CloudVaultSettingsPanel : ComponentBase
{
    private string _name = "";
    private bool _enabled;
    private CloudPolicyMode _mode;
    private int _leaseTtl;
    private int _maxItemBytes;
    private int _maxChannels;
    private int _maxTypes;
    private long _maxTotal;
    private bool _isBusy;

    [Parameter] [EditorRequired] public CloudVault Vault { get; set; } = default!;
    [Parameter] public EventCallback Saved { get; set; }

    [Inject] private UpdateCloudVault UseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _name = Vault.Name;
        _enabled = Vault.IsEnabled;
        _mode = Vault.PolicyMode;
        _leaseTtl = Vault.LeaseTtlMinutes;
        _maxItemBytes = Vault.MaxItemBytes;
        _maxChannels = Vault.MaxChannelsPerPlayer;
        _maxTypes = Vault.MaxTypesPerChannel;
        _maxTotal = Vault.MaxTotalPerChannel;
    }

    private async Task SaveAsync()
    {
        if (_isBusy)
            return;
        _isBusy = true;
        try
        {
            var settings = new CloudVaultSettings(_name, _enabled, _mode, _leaseTtl, _maxItemBytes, _maxChannels,
                _maxTypes, _maxTotal);
            var result = await UseCase.HandleAsync(Vault.Id, settings, CancellationToken.None);
            Snackbar.Add(result.Succeeded ? "Configurações salvas." : result.Error!,
                result.Succeeded ? Severity.Success : Severity.Error);
            if (result.Succeeded)
                await Saved.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
