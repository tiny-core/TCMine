using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Um <see cref="ErrorBoundary" /> comum, só que loga e se reseta sozinho.
///     Existe porque uma exceção não tratada num evento de UI (ex.: o clique de
///     login) nunca chega ao host: no WPF, o BlazorWebView intercepta o
///     circuito internamente e ela desaparece sem log nem diálogo nenhum — o
///     jogador só vê um botão que "não reage". <see cref="ErrorBoundary" /> é a
///     única porta pública do Blazor para isto, e funciona igual em qualquer
///     host (WPF hoje, o que vier depois no Linux).
///     Usado em <c>Routes.razor</c> SEM <c>@key</c> por rota: o <c>RouteView</c>
///     que ele envolve carrega o <c>ShellLayout</c>, e o arranque (handshake,
///     pareamento, sessão) só pode rodar uma vez por sessão — uma chave que
///     mudasse a cada navegação remontaria o layout junto e faria o arranque
///     repetir a cada troca de tela. Em vez disso, o reset é interno: ouve a
///     navegação e chama <see cref="ErrorBoundary.Recover" /> sozinho, sem
///     recriar nada ao redor.
/// </summary>
public sealed partial class LoggingErrorBoundary : ErrorBoundary, IDisposable
{
    [Inject] private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    void IDisposable.Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        InvokeAsync(Recover);

    protected override Task OnErrorAsync(Exception exception)
    {
        LogErroCapturado(Logger, exception);
        return base.OnErrorAsync(exception);
    }

    // Estático com ILogger explícito, e não um campo privado: um componente
    // Blazor recebe a dependência por propriedade [Inject], não por
    // construtor, e o gerador de [LoggerMessage] só encontra campo — não
    // propriedade — quando procura sozinho.
    [LoggerMessage(Level = LogLevel.Error, Message = "Exceção não tratada capturada pelo ErrorBoundary.")]
    private static partial void LogErroCapturado(ILogger logger, Exception ex);
}
