using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;

namespace TCMine.Server.Web.Components.Pages;

public partial class AccessRequestsPage : ComponentBase
{
    private bool _isBusy;
    private bool _loaded;
    private List<AccessRequestView> _requests = [];

    [Inject] private ListAccessRequests ListUseCase { get; set; } = default!;
    [Inject] private ApproveAccessRequest ApproveUseCase { get; set; } = default!;
    [Inject] private DenyAccessRequest DenyUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _requests = [.. await ListUseCase.HandleAsync(CancellationToken.None)];
        _loaded = true;
    }

    private Task ApproveAsync(AccessRequestView request) => RunAsync(async () =>
    {
        var result = await ApproveUseCase.HandleAsync(request.Id, CancellationToken.None);

        if (result.Succeeded)
            Snackbar.Add($"{request.DisplayName} agora tem acesso a {request.ServerName}.", Severity.Success);
        else
            Snackbar.Add(result.Error!, Severity.Error);
    });

    private Task DenyAsync(AccessRequestView request) => RunAsync(async () =>
    {
        var result = await DenyUseCase.HandleAsync(request.Id, CancellationToken.None);

        if (result.Succeeded)
            Snackbar.Add($"Pedido de {request.DisplayName} recusado.", Severity.Success);
        else
            Snackbar.Add(result.Error!, Severity.Error);
    });

    /// <summary>Recarrega sempre ao final: a lista inteira é mais barata de acertar que remendar uma linha.</summary>
    private async Task RunAsync(Func<Task> action)
    {
        if (_isBusy)
            return;

        _isBusy = true;

        try
        {
            await action();
            await LoadAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
