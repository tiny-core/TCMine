using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class ShellLayout : LayoutComponentBase, IDisposable
{
    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private ServerPairing Pairing { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     O arranque mora no layout, e não numa página, porque ele acontece uma
    ///     vez por sessão: o layout não é reconstruído ao navegar, e a checagem
    ///     não pode repetir a cada troca de tela.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        Shell.Changed += OnShellChanged;
        Shell.BeginCheck();

        var estado = await Pairing.ResumeAsync(CancellationToken.None);

        Shell.Apply(estado);

        // Sem servidor conhecido não há nada para mostrar; com servidor conhecido
        // e fora do ar, há — a moldura fica de pé e o aviso explica o resto.
        if (!estado.IsPaired)
            Navigation.NavigateTo("/pair");
    }

    private void OnShellChanged() => InvokeAsync(StateHasChanged);
}
