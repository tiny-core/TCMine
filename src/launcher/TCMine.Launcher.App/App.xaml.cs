using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TCMine.Launcher.App.Chrome;
using TCMine.Launcher.Core;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Infrastructure;
using TCMine.Launcher.Infrastructure.Windows;
using TCMine.Launcher.UI;
using TCMine.Launcher.UI.Abstractions;
using Velopack;
// Serilog também declara um ILogger, e ele não tem nada que ver com o do
// Extensions.Logging que o resto do arquivo usa (LoggerMessage.Define, o campo
// _logger). Mesma armadilha documentada no CLAUDE.md para o LogLevel do MSAL —
// sem o alias, toda referência bare a ILogger neste arquivo vira CS0104.
using ILogger = Microsoft.Extensions.Logging.ILogger;
#if DEBUG
using TCMine.Launcher.App.Dev;
#endif

namespace TCMine.Launcher.App;

/// <summary>
///     Ponto de entrada. Monta o contêiner e abre a janela.
///     Usa o host genérico em vez de um ServiceCollection solto porque o que segue
///     precisa dele: a conexão com o hub e a reconciliação de estado são
///     serviços em background com ciclo de vida próprio, e o host é quem os
///     inicia e para com a aplicação.
/// </summary>
public partial class App : Application
{
    private static readonly Action<ILogger, Exception?> LogStopFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(LogStopFailed)),
            "O host não parou limpo no fecho; a aplicação sai na mesma.");

    private readonly IHost _host;

    // Campo, e não uma resolução dentro do log: CA1873 cobra que o argumento de
    // uma chamada de log seja barato, e GetRequiredService no meio dela não é.
    private readonly ILogger<App> _logger;

    public App()
    {
        // ANTES de tudo o resto, e isto não é preferência: o Velopack usa o
        // próprio executável como ferramenta de instalação e desinstalação, e é
        // esta chamada que intercepta esses arranques. Após abrir uma
        // janela já é tarde — o instalador mostraria a interface do launcher em
        // vez de instalar.
        VelopackApp.Build().Run();

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

        // A raiz da instalação: o tcmine.json, o content store e as instâncias
        // moram aqui, um nível acima da pasta que o autoupdate substitui.
        // Qualificado: num projeto WPF, o Path implícito é o System.Windows.Shapes.
        var raiz = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TCMine");

        // Sem isto, uma falha (ex.: login) só aparecia com um depurador
        // anexado — nenhum arquivo, nenhum jeito de o jogador reportar o que
        // aconteceu. Um arquivo por dia, guardando duas semanas: o suficiente
        // para investigar sem crescer sem limite.
        builder.Services.AddSerilog(config => config
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(raiz, "logs", "launcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                formatProvider: CultureInfo.InvariantCulture));

        builder.Services.AddLauncherInfrastructure(raiz);

        // Substitui as portas que a infraestrutura portável só sabe recusar:
        // o linker que não liga, e o login que ainda não sabia entrar.
        builder.Services.AddWindowsLauncherInfrastructure(raiz);
#if DEBUG
        // Atalho de desenvolvimento, e SÓ quando a variável existe. Registrar
        // sempre passaria por cima do MSAL que acabou de entrar: quem estivesse
        // a trabalhar no login nunca veria o navegador abrir, porque este
        // autenticador responderia primeiro — e responderia "não sei entrar".
        // Registrado DEPOIS da infraestrutura de propósito: o último vence.
        // Em release o bloco inteiro não é compilado.
        if (EnvironmentMinecraftAuthenticator.Disponivel)
            builder.Services.AddSingleton<IMinecraftAuthenticator, EnvironmentMinecraftAuthenticator>();
#endif
        builder.Services.AddLauncherCore();
        builder.Services.AddLauncherUi();
        builder.Services.AddSingleton(BuildInfo());

        builder.Services.AddSingleton<MainWindow>();

        // A moldura precisa da janela, e a janela é resolvida pelo contêiner.
        // Não há ciclo: quem pede IWindowChrome é a barra de título, que só
        // renderiza após a janela existir.
        builder.Services.AddSingleton<IDesktopShell, WpfDesktopShell>();

        builder.Services.AddSingleton<IWindowChrome>(sp =>
            new WpfWindowChrome(sp.GetRequiredService<MainWindow>()));

        // O pai do diálogo do broker. Mesma resolução tardia da moldura, e pelo
        // mesmo motivo: a janela é resolvida pelo contêiner e só existe depois.
        builder.Services.AddSingleton<IParentWindowHandle>(sp =>
            new WpfParentWindowHandle(
                sp.GetRequiredService<MainWindow>(),
                sp.GetRequiredService<ILogger<WpfParentWindowHandle>>()));

        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<App>>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            await _host.StartAsync();

            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception err)
        {
            MessageBox.Show(err.Message);
        }
    }

    /// <summary>
    ///     Para o host antes de o processo sair. SÍNCRONO, e era async void.
    ///     O WPF não espera por um <c>async void</c>: ele seguia para o fecho
    ///     assim que este método atingia o primeiro await, e o <c>Dispose</c>
    ///     depois dele podia simplesmente nunca correr.
    ///     O <c>Task.Run</c> tira a espera do dispatcher. Bloquear a thread de UI
    ///     à espera de algo que precise dela seria um deadlock no fecho — a pior
    ///     altura para o ter, porque não há mais interface para o mostrar.
    ///     Com prazo, porque nada aqui vale prender o fecho para sempre.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Task.Run(() => _host.StopAsync(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogStopFailed(_logger, ex);
        }

        // _host.Dispose();

        base.OnExit(e);

        // Garante que o processo morre. O WebView2 pode deixar threads/COM
        // nativos vivos que o encerramento normal do .NET espera para sempre
        // — era exatamente isso que fazia o TCMine.Launcher.App sobreviver à
        // janela no gestor de tarefas, mesmo com o host já parado e a janela
        // já fechada. Tudo que precisava rodar de propósito já rodou (host
        // parado e descartado, base.OnExit chamado); daqui para a frente só
        // interessa que o processo suma — e nada além do Environment.Exit
        // garante isso contra algo que o WebView2 esteja segurando por fora
        // do controle do runtime gerenciado.
        Environment.Exit(0);
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

        var version = assembly
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                          .InformationalVersion
                      ?? "0.0.0-dev";

        return new LauncherAppInfo { Title = "TCMine Launcher", Version = version };
    }
}
