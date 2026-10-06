using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class TitleBar : ComponentBase, IDisposable
{
    [Inject] private IWindowChrome Chrome { get; set; } = default!;

    [Inject] private LauncherAppInfo AppInfo { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    /// <summary>
    ///     O LauncherConfig chama isto de "nome exibido na janela": um jogador
    ///     com dois servidores pareados em máquinas diferentes precisa saber qual
    ///     janela é qual. O nome do produto fica, porque a janela também aparece
    ///     na barra de tarefas ao lado de tudo o mais.
    /// </summary>
    private string Title => Shell.ServerName is { Length: > 0 } server
        ? $"{AppInfo.Title} — {server}"
        : AppInfo.Title;

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        // O nome do servidor só chega depois do handshake; sem ouvir o shell,
        // a barra de título ficaria com o nome genérico até a próxima navegação.
        Shell.Changed += OnShellChanged;
    }

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

    private void OnShellChanged() => InvokeAsync(StateHasChanged);
}
