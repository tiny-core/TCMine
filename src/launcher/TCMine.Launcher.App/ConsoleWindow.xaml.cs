using System.ComponentModel;
using System.Windows;

namespace TCMine.Launcher.App;

/// <summary>
///     A janela do console: um segundo WebView com a raiz <c>ConsoleRoot</c>.
///     Fechar ESCONDE em vez de destruir. Criar um WebView2 custa perto de um
///     segundo, e o console é aberto e fechado muitas vezes numa sessão; vivo,
///     ele também continua a acompanhar o log enquanto está escondido, e abre
///     já em dia.
/// </summary>
public partial class ConsoleWindow : Window
{
    private bool _closingForGood;

    public ConsoleWindow(IServiceProvider services)
    {
        // Mesmo caminho da MainWindow: o BlazorWebView lê o provedor por
        // {DynamicResource}, antes de existir DataContext.
        Resources.Add("services", services);

        InitializeComponent();
    }

    /// <summary>Fecha de verdade — a aplicação está a sair.</summary>
    public void CloseForGood()
    {
        _closingForGood = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // A aplicação a sair fecha a janela de verdade; o jogador a fechar só a
        // esconde. Cancelar durante o encerramento seguraria o processo vivo.
        if (!_closingForGood && !Dispatcher.HasShutdownStarted)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
