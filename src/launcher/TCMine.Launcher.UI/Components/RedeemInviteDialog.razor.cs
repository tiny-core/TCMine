using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Pede o código que o administrador enviou e troca por acesso ao servidor.
///     O servidor diz exatamente o que deu errado (código inválido, expirado, já
///     usado) — a mensagem vem direto dele, sem reinterpretação aqui.
/// </summary>
public partial class RedeemInviteDialog : ComponentBase
{
    private string _code = "";
    private string? _error;
    private bool _submitting;

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Inject] private RedeemInvite UseCase { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    private async Task ResgatarAsync()
    {
        if (Shell.Pairing?.Config is not { } config || _submitting)
            return;

        _submitting = true;
        _error = null;

        try
        {
            var result = await UseCase.HandleAsync(config.ServerUrl, _code, CancellationToken.None);

            if (result.Succeeded)
                Dialog.Close(DialogResult.Ok(true));
            else
                _error = result.Error;
        }
        finally
        {
            _submitting = false;
        }
    }

    private void Cancelar() => Dialog.Cancel();
}
