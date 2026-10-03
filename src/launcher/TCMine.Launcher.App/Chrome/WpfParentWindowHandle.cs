using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Infrastructure.Windows;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     O HWND da janela principal, para o broker do Windows desenhar sobre ela.
///     Lê a cada chamada em vez de guardar: o handle só existe após a
///     janela ser mostrada, e o login silencioso do arranque corre antes disso.
///     Guardá-lo no construtor devolveria zero para sempre.
///     <c>EnsureHandle()</c>, e não <c>Handle</c>: a janela estar visível não
///     garante que o HWND nativo já foi criado (a fonte HWND pode não estar
///     realizada ainda, mesmo depois do <c>Show()</c>) — ler <c>Handle</c>
///     direto podia devolver zero mesmo com a janela na tela, e o MSAL falha
///     com "A window handle must be configured" quando isso acontece.
///     <c>EnsureHandle()</c> força a criação na hora, é idempotente, e é a
///     correção que a própria mensagem do MSAL aponta
///     (aka.ms/msal-net-wam#parent-window-handles).
/// </summary>
internal sealed partial class WpfParentWindowHandle(
    MainWindow window,
    ILogger<WpfParentWindowHandle> logger) : IParentWindowHandle
{
    public nint Handle
    {
        get
        {
            var handle = window.Dispatcher.Invoke(() => new WindowInteropHelper(window).EnsureHandle());

            // Diagnóstico temporário: o MSAL recusa com "window_handle_required"
            // mesmo depois do EnsureHandle(), e só isto diz se o valor que
            // chega até ele é mesmo zero ou se o problema está no broker.
            LogHandleResolvido(handle);

            return handle;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HWND resolvido para o broker: {Handle}.")]
    private partial void LogHandleResolvido(nint handle);
}
