using Microsoft.AspNetCore.Components;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.UI.Pages;

public partial class AboutPage : ComponentBase
{
    private const string RepositoryUrl = "https://github.com/tiny-core/TCMine";

    [Inject] private LauncherAppInfo AppInfo { get; set; } = default!;

    [Inject] private IDesktopShell Desktop { get; set; } = default!;

    // Síncrono por baixo (Process.Start), mas OnClick de MudButton pede um
    // Task — e chamar de uma tela que vive dentro do WebView2 é o motivo de
    // IDesktopShell existir: um <a href> aqui navegaria a própria janela para
    // o GitHub em vez de abrir o navegador do sistema.
    private Task OpenRepositoryAsync()
    {
        Desktop.OpenUrl(RepositoryUrl);
        return Task.CompletedTask;
    }
}
