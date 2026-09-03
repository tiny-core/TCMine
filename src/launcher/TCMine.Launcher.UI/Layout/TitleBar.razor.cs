using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.UI.Layout;

public partial class TitleBar : ComponentBase, IDisposable
{
    [Inject] private IWindowChrome Chrome { get; set; } = default!;

    [Inject] private LauncherAppInfo AppInfo { get; set; } = default!;

    public void Dispose()
    {
        Chrome.StateChanged -= OnWindowStateChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Chrome.StateChanged += OnWindowStateChanged;

    /// <summary>
    ///     Só o botão esquerdo arrasta. Sem o filtro, um clique com o direito
    ///     entregaria o ponteiro ao Windows e engoliria o menu de contexto da
    ///     janela, que é justamente o que o botão direito deveria abrir.
    /// </summary>
    private void OnPointerDown(MouseEventArgs e)
    {
        if (e.Button is 0)
            Chrome.BeginDrag();
    }

    /// <summary>
    ///     A janela pode ser maximizada por fora daqui (Win+seta, arrastar para o
    ///     topo). Sem redesenhar, o ícone do botão ficaria descrito ao contrário.
    /// </summary>
    private void OnWindowStateChanged() => InvokeAsync(StateHasChanged);
}
