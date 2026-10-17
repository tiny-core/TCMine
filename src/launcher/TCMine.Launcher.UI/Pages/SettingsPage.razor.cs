using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class SettingsPage : ComponentBase, IDisposable
{
    /// <summary>Nulo até ler o disco, para o interruptor não nascer na posição errada.</summary>
    private LauncherPreferences? _preferences;

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

    [Inject] private ILauncherPreferences Preferences { get; set; } = default!;

    private bool SyncAll => _preferences?.SyncAllOptions ?? false;

    public void Dispose()
    {
        Lock.Changed -= OnLockChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        Lock.Changed += OnLockChanged;
        _preferences = await Preferences.ReadAsync(CancellationToken.None);
    }

    /// <summary>
    ///     Grava na hora: é um interruptor, e um botão "Guardar" à parte seria
    ///     um passo a esquecer. Vale a partir da próxima abertura do jogo.
    /// </summary>
    private async Task SetSyncAllAsync(bool value)
    {
        if (_preferences is null)
            return;

        _preferences = _preferences with { SyncAllOptions = value };
        await Preferences.WriteAsync(_preferences, CancellationToken.None);
    }

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
