using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.UI.Pages;

public partial class InstancesPage : ComponentBase
{
    private bool _busy;
    private IReadOnlyList<InstalledInstance> _instances = [];
    private bool _loading = true;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private IDesktopShell Desktop { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _instances = await Instances.HandleAsync(CancellationToken.None);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OpenFolder(InstalledInstance instancia) => Desktop.OpenFolder(instancia.Path);

    /// <summary>
    ///     Confirma antes de apagar, e o texto diz o que se perde.
    ///     Remover leva o mundo do jogador junto — é a única ação do launcher
    ///     que destrói algo que não dá para baixar de novo.
    /// </summary>
    private async Task RemoveAsync(InstalledInstance instancia)
    {
        var confirmado = await Dialogs.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = "Remover instância",
            MarkupMessage = new MarkupString(
                $"Isto apaga <b>{instancia.Manifest.ModpackName}</b> e tudo que está na pasta dela, "
                + "<b>inclusive os mundos</b> criados nesta instância.<br/><br/>"
                + "Os mods continuam no store compartilhado e não precisarão ser baixados de novo."),
            YesText = "Remover",
            CancelText = "Cancelar"
        });

        if (confirmado is not true)
            return;

        _busy = true;

        try
        {
            await Instances.RemoveAsync(instancia, CancellationToken.None);

            Snackbar.Add($"{instancia.Manifest.ModpackName} removido.", Severity.Success);

            await LoadAsync();
        }
        finally
        {
            _busy = false;
        }
    }
}
