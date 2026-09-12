using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.UI.Pages;

public partial class InstancesPage : ComponentBase
{
    private InstanceKey? _active;
    private bool _busy;
    private IReadOnlyList<InstalledInstance> _instances = [];
    private bool _loading = true;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

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

            // A ativa vem resolvida, e não lida em bruto: com uma instância só
            // ela é a ativa sem ninguém ter escolhido, e a etiqueta tem de dizer
            // o mesmo que a tela de jogar mostra.
            _active = (await Active.CurrentAsync(CancellationToken.None)).Active?.Key;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OpenFolder(InstalledInstance instancia) => Desktop.OpenFolder(instancia.Path);

    /// <summary>
    ///     Marca como ativa e leva para a tela de jogar.
    ///     Navegar faz parte da ação: o clique é "quero jogar esta", e deixar o
    ///     jogador na lista depois de escolher obrigaria-o a descobrir sozinho
    ///     que o resultado está noutro sítio.
    /// </summary>
    private async Task ActivateAsync(InstalledInstance instancia)
    {
        _busy = true;

        try
        {
            await Active.SetAsync(instancia.Key, CancellationToken.None);
            _active = instancia.Key;
        }
        finally
        {
            _busy = false;
        }

        Navigation.NavigateTo("/");
    }

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
