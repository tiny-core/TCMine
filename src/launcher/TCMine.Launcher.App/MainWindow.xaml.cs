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
    ///     Melhor esforço para descartar o WebView2 ao fechar — sem apostar
    ///     nisso para o processo morrer.
    ///     Já tentei esperar o Dispose terminar aqui (síncrono via Task.Run, e
    ///     depois via um DispatcherFrame aninhado) e as duas vezes o processo
    ///     sobreviveu à janela mesmo assim: o botão de fechar chega até aqui
    ///     de dentro de um Dispatcher.Invoke que o próprio WebView2 disparou
    ///     (Chrome.Close, na barra de título em Blazor), então qualquer bomba
    ///     de mensagens aninhada que eu monte aqui corre por cima da pilha de
    ///     reentrância do WebView2 — terreno instável demais para apostar a
    ///     morte do processo nisso. Quem garante o processo sumir do gestor de
    ///     tarefas é o Environment.Exit em App.OnExit; isto aqui só dá ao
    ///     WebView2 a chance de fechar sozinho, sem travar o fecho da janela
    ///     esperando por ele.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            // .AsTask(): descartar a ValueTask direto dispara CA2012 (ela só
            // pode ser observada uma vez, e um object pool por baixo dela
            // tornaria o descarte um bug de verdade). Task aceita descarte
            // fire-and-forget sem ambiguidade nenhuma.
            _ = ((IAsyncDisposable)WebView).DisposeAsync().AsTask();
        }
        catch
        {
            // Já em queda. Insistir não torna o fecho mais limpo.
        }

        // Watchdog de último recurso. Medido em produção: fechar pelo botão da
        // barra de título (Blazor → IWindowChrome.Close → Dispatcher.Invoke)
        // faz este OnClosed rodar de DENTRO do laço de mensagens nativo que o
        // próprio WebView2 abriu para entregar o clique. O pedido de shutdown
        // que o WPF enfileira ao fechar a última janela só é processado
        // quando esse laço devolve o controle ao Dispatcher normal — e às
        // vezes isso nunca acontece, então App.OnExit (onde mora o
        // Environment.Exit que devia matar o processo) nunca chega a rodar. O
        // TCMine.Launcher.App ficava para sempre no Gerenciador de Tarefas,
        // mesmo com a janela já fechada e nenhum erro na tela.
        // Em vez de confiar que Close → Closed → Shutdown → OnExit termina de
        // se propagar, este watchdog roda FORA do dispatcher (Task.Run, não
        // Dispatcher.Invoke) e mata o processo sozinho se o caminho normal não
        // chegar lá primeiro. Os três segundos são folga para o caminho feliz
        // (App.OnExit, que para o host direito) vencer a corrida quando ele
        // funciona; quando não funciona, é a garantia de que o processo não
        // fica para trás de qualquer forma.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            Environment.Exit(0);
        });

        base.OnClosed(e);
    }
}
