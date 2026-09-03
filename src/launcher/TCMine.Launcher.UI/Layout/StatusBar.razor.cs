using Microsoft.AspNetCore.Components;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class StatusBar : ComponentBase, IDisposable
{
    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private LauncherAppInfo AppInfo { get; set; } = default!;

    private string ConnectionLabel => Shell.Connection switch
    {
        ConnectionState.Connected => $"Ligado a {Shell.ServerName ?? "servidor"}",
        ConnectionState.Connecting => "A verificar ligação…",

        // "Sem servidor" e "sem ligação" pedem ações diferentes: uma é parear,
        // a outra é esperar. Um rótulo só para os dois casos mandaria o jogador
        // redigitar um endereço que está correto.
        _ => Shell.IsPaired ? "Sem ligação" : "Sem servidor"
    };

    private string DotClass => Shell.Connection switch
    {
        ConnectionState.Connected => "tc-dot-online",
        ConnectionState.Connecting => "tc-dot-pending",
        _ => "tc-dot-offline"
    };

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Shell.Changed += OnShellChanged;

    private void OnShellChanged() => InvokeAsync(StateHasChanged);
}
