using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Aba "Quarentena": lotes recusados, para o dono aplicar ou descartar.</summary>
public partial class CloudQuarantinePanel : ComponentBase
{
    private bool _isBusy;
    private List<CloudQuarantineView>? _items;
    private bool _showResolved;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] public IReadOnlyDictionary<Guid, string> ServerNames { get; set; } = new Dictionary<Guid, string>();
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private ListCloudQuarantine ListUseCase { get; set; } = default!;
    [Inject] private ResolveCloudQuarantine ResolveUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(VaultId, !_showResolved, CancellationToken.None);
        _items = result.Succeeded ? [.. result.Value!] : [];
    }

    private async Task ResolveAsync(CloudQuarantineView item, bool apply)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(apply ? "Aplicar lote" : "Descartar lote",
            apply
                ? "Os saldos mudam como o lote dizia. Se o lote for repetido ou inventado, isso cria itens."
                : "O lote nunca entra. Os itens que o servidor achava ter guardado não voltam.",
            apply ? "Aplicar" : "Descartar", cancelText: "Cancelar");
        if (confirm is not true || _isBusy) return;

        _isBusy = true;
        try
        {
            var result = await ResolveUseCase.HandleAsync(VaultId, item.Quarantine.Id, apply, CancellationToken.None);
            Snackbar.Add(result.Succeeded ? apply ? "Lote aplicado." : "Lote descartado." : result.Error!,
                result.Succeeded ? Severity.Success : Severity.Error);
            await LoadAsync();
            await Changed.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
