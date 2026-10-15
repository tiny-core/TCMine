using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.UI.Components;

public partial class ServerAccessButton : ComponentBase
{
    private bool _requesting;

    [Parameter] [EditorRequired] public GameServerDto Server { get; set; } = default!;

    [Parameter] public Variant Variant { get; set; } = Variant.Text;

    [Parameter] public Size Size { get; set; } = Size.Small;

    [Inject] private IServerConnection Connection { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>
    ///     Pendente de verdade (o servidor já respondeu "Pending") OU acabou de
    ///     ser pedido aqui — sem isto, o botão voltaria a aparecer "Pedir
    ///     acesso" entre o clique e o catálogo recarregar, parecendo que o
    ///     pedido não foi enviado.
    /// </summary>
    private bool IsPending => Server.AccessState is ServerAccessState.Pending || Requested;

    /// <summary>Pedido feito por este botão, nesta tela.</summary>
    private bool Requested { get; set; }

    private async Task RequestAsync()
    {
        if (_requesting)
            return;

        _requesting = true;

        try
        {
            await Connection.RequestServerAccessAsync(Server.Id, CancellationToken.None);
            Requested = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Snackbar.Add($"Não foi possível pedir acesso: {ex.Message}", Severity.Error);
        }
        finally
        {
            _requesting = false;
        }
    }
}
