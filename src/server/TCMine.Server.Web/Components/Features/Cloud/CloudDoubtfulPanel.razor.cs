using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Aba "Em dúvida": operações talvez perdidas numa queda, para devolver ou dispensar.</summary>
public partial class CloudDoubtfulPanel : ComponentBase
{
    private bool _isBusy;
    private List<CloudDoubtfulOperation>? _items;
    private bool _showResolved;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] public IReadOnlyDictionary<Guid, string> ServerNames { get; set; } = new Dictionary<Guid, string>();
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private ListCloudDoubtful ListUseCase { get; set; } = default!;
    [Inject] private ResolveCloudDoubtful ResolveUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(VaultId, !_showResolved, CancellationToken.None);
        _items = result.Succeeded ? [.. result.Value!] : [];
    }

    private async Task ResolveAsync(CloudDoubtfulOperation op, bool refund)
    {
        if (refund)
        {
            var confirm = await DialogService.ShowMessageBoxAsync("Devolver itens",
                $"{op.Amount} itens voltam para a nuvem do jogador. Você conferiu que ele está sem eles no jogo?",
                "Devolver", cancelText: "Cancelar");
            if (confirm is not true) return;
        }

        if (_isBusy) return;

        _isBusy = true;
        try
        {
            var result = await ResolveUseCase.HandleAsync(VaultId, op.Id, refund, CancellationToken.None);
            Snackbar.Add(result.Succeeded ? refund ? "Itens devolvidos." : "Operação dispensada." : result.Error!,
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
