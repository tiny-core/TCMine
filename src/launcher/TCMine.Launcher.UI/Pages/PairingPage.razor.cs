using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class PairingPage : ComponentBase
{
    private string _address = "";
    private bool _busy;
    private string? _message;
    private Severity _severity = Severity.Error;

    [Inject] private ServerPairing Pairing { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        // Enter num formulário de um campo só é o que qualquer um espera. Sem
        // isto o jogador digita o endereço e fica olhando para a tela.
        if (e.Key is "Enter" && !_busy && !string.IsNullOrWhiteSpace(_address))
            await PairAsync();
    }

    private async Task PairAsync()
    {
        _busy = true;
        _message = null;

        try
        {
            var estado = await Pairing.PairAsync(_address, CancellationToken.None);

            Shell.Apply(estado);

            if (estado.IsOnline)
            {
                Navigation.NavigateTo("/");
                return;
            }

            _message = estado.Message ?? "Não foi possível ligar a este endereço.";

            // Incompatibilidade não é erro do jogador nem falha de rede: o
            // endereço está certo e alguém precisa atualizar alguma coisa.
            // Pintar de vermelho sugeriria tentar de novo, que não resolve.
            _severity = estado.Status is PairingStatus.Incompatible
                ? Severity.Warning
                : Severity.Error;
        }
        finally
        {
            _busy = false;
        }
    }
}
