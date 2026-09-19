using System;
using System.Threading.Tasks;
using System.Windows;

namespace TCMine.Launcher.App;

/// <summary>
///     A janela, e nada além dela: um WebView2 ocupando tudo e a moldura
///     desligada. Toda a interface — barra de título inclusive — é desenhada
///     pelas telas de TCMine.Launcher.UI.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider services)
    {
        // O BlazorWebView pede o provedor por {DynamicResource}, e não por
        // binding: ele é resolvido na inicialização do controle, antes de existir
        // DataContext. Depositar no dicionário de recursos da janela é o caminho
        // que a documentação do Blazor Hybrid usa.
        Resources.Add("services", services);

        InitializeComponent();
    }

    /// <summary>
    ///     Descarta o WebView2 ao fechar.
    ///     Sem isto o processo sobrevive à janela: o BlazorWebView segura o
    ///     ambiente do WebView2 e o gestor do Blazor, e nenhum deles cai sozinho
    ///     quando a janela some — o jogador fecha, a janela desaparece e o
    ///     TCMine.Launcher.App continua no gestor de tarefas, impedindo inclusive
    ///     a próxima abertura de se comportar como primeira.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        // IAsyncDisposable, não IDisposable — e descartado ANTES de o fecho
        // seguir, porque é o WebView que segura o processo. O Task.Run tira a
        // espera do dispatcher: bloqueá-lo aqui, à espera de algo que pode
        // precisar dele, seria um deadlock no fecho.
        try
        {
            Task.Run(async () => await ((IAsyncDisposable)WebView).DisposeAsync())
                .Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Já em queda. Insistir não torna o fecho mais limpo.
        }

        base.OnClosed(e);
    }
}
