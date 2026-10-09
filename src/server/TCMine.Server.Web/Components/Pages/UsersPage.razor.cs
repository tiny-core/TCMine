using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Web.Components.Features.Account;

namespace TCMine.Server.Web.Components.Pages;

public partial class UsersPage : ComponentBase
{
    private bool _canManage;
    private bool _isBusy;
    private bool _loaded;
    private string _search = "";
    private List<User> _users = [];

    [Inject] private ListUsers ListUseCase { get; set; } = default!;
    [Inject] private SetInstanceAdmin SetAdminUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private IEnumerable<User> _filtrados => string.IsNullOrWhiteSpace(_search)
        ? _users
        : _users.Where(u => u.DisplayName.Contains(_search, StringComparison.OrdinalIgnoreCase));

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(CancellationToken.None);

        // A recusa do caso de uso é a fonte da verdade sobre poder gerenciar —
        // mesma regra do ServerMembersPanel: perguntar o papel aqui, à parte,
        // criaria uma segunda checagem livre para divergir da primeira.
        _canManage = result.Succeeded;
        _loaded = true;

        if (result.Succeeded)
            _users = [.. result.Value!];
    }

    private static string TipoDeConta(User user) => (user.MicrosoftObjectId, user.MinecraftUuid) switch
    {
        (not null, not null) => "Microsoft + Minecraft",
        (not null, null) => "Microsoft",
        (null, not null) => "Minecraft (launcher)",
        _ => "—"
    };

    private async Task ToggleAdminAsync(User user, bool ligado)
    {
        if (_isBusy)
            return;

        _isBusy = true;

        try
        {
            var result = await SetAdminUseCase.HandleAsync(user.Id, ligado, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add($"{user.DisplayName}: {(ligado ? "promovido a" : "removido de")} admin.",
                    Severity.Success);
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);

            await LoadAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ShowMembershipsAsync(User user)
    {
        var parameters = new DialogParameters<UserMembershipsDialog>
        {
            { x => x.UserId, user.Id }, { x => x.DisplayName, user.DisplayName }
        };

        var dialog = await DialogService.ShowAsync<UserMembershipsDialog>(
            $"Vínculos de {user.DisplayName}", parameters);

        await dialog.Result;
    }
}
