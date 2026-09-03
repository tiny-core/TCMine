using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class SettingsPage : ComponentBase
{
    private bool _signingOut;

    [Inject] private LauncherAppInfo AppInfo { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private SignIn Account { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private async Task SignOutAsync()
    {
        if (Shell.Pairing?.Config is not { } config)
            return;

        _signingOut = true;

        try
        {
            Shell.Apply(await Account.SignOutAsync(config, CancellationToken.None));

            // O trilho some junto com a sessão, então ficar nesta página deixaria
            // o jogador sem navegação nenhuma.
            Navigation.NavigateTo("/login");
        }
        finally
        {
            _signingOut = false;
        }
    }
}
