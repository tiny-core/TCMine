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
}
