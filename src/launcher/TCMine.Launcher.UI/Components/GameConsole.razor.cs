using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.UI.Components;

public partial class GameConsole : ComponentBase, IDisposable
{
    [Inject] private GameSession Game { get; set; } = default!;

    [Inject] private IDesktopShell Desktop { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private string Title =>
        Game.LastInstance?.Manifest is { } manifest
            ? manifest.ServerName is { } server ? $"{manifest.ModpackName} · {server}" : manifest.ModpackName
            : "Console do jogo";

    /// <summary>
    ///     Deixa de ouvir a sessão quando a janela fecha — o GameSession vive
    ///     tanto quanto a aplicação, e um assinante esquecido redesenharia uma
    ///     árvore morta a cada linha de log.
    /// </summary>
    public void Dispose()
    {
        Game.Changed -= OnGameChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Game.Changed += OnGameChanged;

    /// <summary>Vem do fio que lê o processo, por isso o InvokeAsync.</summary>
    private void OnGameChanged() => InvokeAsync(StateHasChanged);

    /// <summary>
    ///     Na ordem em que o jogo escreveu, e não na da tela: quem cola o log
    ///     num pedido de ajuda espera lê-lo de cima para baixo.
    /// </summary>
    private async Task CopyAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", string.Join('\n', Game.Log));
            Snackbar.Add("Registro copiado.", Severity.Success);
        }
        catch (JSException ex)
        {
            Snackbar.Add($"Não foi possível copiar: {ex.Message}", Severity.Error);
        }
    }

    private void OpenLogs()
    {
        if (Game.LastInstance is { } instance)
            Desktop.OpenFolder(Path.Combine(instance.Path, "logs"));
    }
}
