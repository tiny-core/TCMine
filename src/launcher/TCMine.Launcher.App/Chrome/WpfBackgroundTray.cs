using System.ComponentModel;
using System.IO;
using System.Windows;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Abstractions;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     Fechar a janela com o jogo aberto manda o launcher para a bandeja.
///     O launcher não é só uma janela enquanto o jogo corre: é ele que segura o
///     túnel da Cloudflare (quando a entrada foi por ele) e que lê o log. Fechar
///     de verdade cortaria os dois — e o jogador que só queria tirar a janela da
///     frente sairia do servidor sem saber porquê.
///     Sem jogo aberto, fechar é fechar, como sempre.
/// </summary>
internal sealed class WpfBackgroundTray : IDisposable
{
    private readonly GameSession _game;
    private readonly IWindowChrome _chrome;
    private readonly Window _window;

    private bool _hintShown;
    private Forms.NotifyIcon? _icon;
    private bool _quitting;

    public WpfBackgroundTray(MainWindow window, GameSession game, IWindowChrome chrome)
    {
        _window = window;
        _game = game;
        _chrome = chrome;

        _window.Closing += OnClosing;
        _window.IsVisibleChanged += OnVisibleChanged;

        // Ícone que fica para trás na bandeja depois de o processo sair é o
        // clássico "fantasma" que só some ao passar o mouse.
        _window.Closed += (_, _) => Dispose();
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_quitting || !_game.IsRunning)
            return;

        e.Cancel = true;
        _window.Hide();
        ShowIcon();
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // De volta à vista (pelo ícone, ou pelo jogo a fechar): o ícone sai.
        if (_window.IsVisible && _icon is not null)
            _icon.Visible = false;
    }

    private void ShowIcon()
    {
        _icon ??= CreateIcon();
        _icon.Visible = true;

        // Uma vez por sessão: a primeira vez é a que surpreende ("fechei e o
        // jogo continua?"); depois o jogador já sabe onde o launcher foi parar.
        if (_hintShown)
            return;

        _hintShown = true;
        _icon.ShowBalloonTip(
            5000,
            "TCMine continua aberto",
            "O launcher fica aqui enquanto o jogo estiver aberto. Clique para voltar a ele.",
            Forms.ToolTipIcon.Info);
    }

    private Forms.NotifyIcon CreateIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir o TCMine", null, (_, _) => _chrome.Restore());
        menu.Items.Add("Sair do TCMine", null, (_, _) => Quit());

        var icon = new Forms.NotifyIcon
        {
            Text = "TCMine Launcher",
            Icon = LoadIcon(),
            ContextMenuStrip = menu
        };

        icon.DoubleClick += (_, _) => _chrome.Restore();
        icon.BalloonTipClicked += (_, _) => _chrome.Restore();

        return icon;
    }

    /// <summary>
    ///     O mesmo icon.ico da janela, que o csproj copia para a pasta do
    ///     executável. O ícone genérico do sistema fica de reserva: sem ícone,
    ///     o NotifyIcon nem aparece.
    /// </summary>
    private static Drawing.Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        return File.Exists(path) ? new Drawing.Icon(path) : Drawing.SystemIcons.Application;
    }

    /// <summary>
    ///     Sair de verdade. O jogo continua aberto (o launcher seguinte o
    ///     reencontra); só se a entrada foi pelo túnel é que sair o tira do
    ///     servidor, e aí pergunta-se antes.
    /// </summary>
    private void Quit()
    {
        if (_game.IsRunning && _game.UsesTunnel)
        {
            var answer = MessageBox.Show(
                "O jogo está ligado ao servidor pelo túnel do TCMine. Sair do launcher derruba essa ligação, "
                + "e o jogo sai do servidor.\n\nSair mesmo assim?",
                "TCMine Launcher",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer is not MessageBoxResult.Yes)
                return;
        }

        _quitting = true;
        _window.Close();
    }
}
