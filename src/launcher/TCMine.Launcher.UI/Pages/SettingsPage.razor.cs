using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class SettingsPage : ComponentBase, IDisposable
{
    private bool _signingOut;

    /// <summary>
    ///     Sair com o jogo aberto deixaria o jogo a correr com uma sessão que já
    ///     não existe; o botão espera o jogo fechar.
    /// </summary>
    [Inject]
    private ActionLock Lock { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private SignIn Account { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    public void Dispose()
    {
        Lock.Changed -= OnLockChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Lock.Changed += OnLockChanged;

    private void OnLockChanged() => InvokeAsync(StateHasChanged);

    private async Task SignOutAsync()
    {
        if (Shell.Pairing?.Config is not { } config || Lock.IsLocked)
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
