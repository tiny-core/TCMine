using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class StatusBar : ComponentBase, IDisposable
{
    private bool _gameWasRunning;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private LauncherAppInfo AppInfo { get; set; } = default!;

    [Inject] private IConsoleWindow ConsoleWindow { get; set; } = default!;

    [Inject] private GameSession Game { get; set; } = default!;

    private string ConnectionLabel => Shell.Connection switch
    {
        ConnectionState.Connected => $"Ligado a {Shell.ServerName ?? "server"}",
        ConnectionState.Connecting => "A verificar ligação…",

        // Diferente de "Sem ligação" de propósito: aqui o jogador não precisa
        // fazer nada, e dizer isso evita que ele feche o launcher para "consertar".
        ConnectionState.Reconnecting => "Sem ligação. A tentar de novo…",

        // "Sem servidor" e "sem ligação" pedem ações diferentes: uma é parear,
        // a outra é esperar. Um rótulo só para os dois casos mandaria o jogador
        // redigitar um endereço que está correto.
        _ => Shell.IsPaired ? "Sem ligação" : "Sem servidor"
    };

    private string DotClass => Shell.Connection switch
    {
        ConnectionState.Connected => "tc-dot-online",
        ConnectionState.Connecting or ConnectionState.Reconnecting => "tc-dot-pending",
        _ => "tc-dot-offline"
    };

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        Game.Changed -= OnGameChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        Shell.Changed += OnShellChanged;
        Game.Changed += OnGameChanged;
    }

    /// <summary>
    ///     Só quando abre ou fecha: o Changed do jogo dispara a cada linha de
    ///     log, e o rodapé só mostra se há jogo aberto.
    /// </summary>
    private void OnGameChanged()
    {
        if (Game.IsRunning == _gameWasRunning)
            return;

        _gameWasRunning = Game.IsRunning;
        InvokeAsync(StateHasChanged);
    }

    private void OnShellChanged() => InvokeAsync(StateHasChanged);
}
