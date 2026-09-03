using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.App.Chrome;
using TCMine.Launcher.UI;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.App;

/// <summary>
///     Ponto de entrada. Monta o contêiner e abre a janela.
///     Usa o host genérico em vez de um ServiceCollection solto porque o que vem
///     a seguir precisa dele: a conexão com o hub e a reconciliação de estado são
///     serviços em background com ciclo de vida próprio, e o host é quem os
///     inicia e para junto com a aplicação.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    // Campo, e não uma resolução dentro do log: CA1873 cobra que o argumento de
    // uma chamada de log seja barato, e GetRequiredService no meio dela não é.
    private readonly ILogger<App> _logger;

    public App()
    {
        // ContentRootPath explícito: aberto pelo atalho do menu Iniciar, o
        // diretório atual do processo é o que o Explorer decidir, e a
        // configuração seria procurada no lugar errado.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Services.AddWpfBlazorWebView();
#if DEBUG
        // Abre o DevTools do WebView2 com F12. Só em Debug: numa build de
        // release seria uma porta aberta para inspecionar a sessão do jogador.
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        builder.Services.AddLauncherUi();
        builder.Services.AddSingleton(BuildInfo());

        builder.Services.AddSingleton<MainWindow>();

        // A moldura precisa da janela, e a janela é resolvida pelo contêiner.
        // Não há ciclo: quem pede IWindowChrome é a barra de título, que só
        // renderiza depois de a janela existir.
        builder.Services.AddSingleton<IWindowChrome>(sp =>
            new WpfWindowChrome(sp.GetRequiredService<MainWindow>()));

        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<App>>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    ///     Uma exceção não tratada na thread de UI derruba a aplicação sem dizer
    ///     nada — e o jogador só vê o launcher sumir. Registrar e avisar é o
    ///     mínimo para o relato de suporte ter alguma informação.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFalhaNaInterface(e.Exception);

        MessageBox.Show(
            $"O launcher encontrou um erro inesperado e vai fechar.\n\n{e.Exception.Message}",
            "TCMine Launcher",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    // Source generator, como manda o CLAUDE.md: chamar LogCritical direto viola
    // CA1848 e aloca o array de parâmetros mesmo quando o nível está desligado.
    [LoggerMessage(Level = LogLevel.Critical, Message = "Falha não tratada na interface.")]
    private partial void LogFalhaNaInterface(Exception ex);

    /// <summary>
    ///     A versão informativa é a que o CI carimba na build (inclui o sufixo de
    ///     canal e o commit). O <c>AssemblyVersion</c> não serve: ele é truncado
    ///     para quatro números e perderia justamente essa parte.
    /// </summary>
    private static LauncherAppInfo BuildInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var versao = assembly
                         .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                         .InformationalVersion
                     ?? "0.0.0-dev";

        return new LauncherAppInfo { Title = "TCMine Launcher", Version = versao };
    }
}
