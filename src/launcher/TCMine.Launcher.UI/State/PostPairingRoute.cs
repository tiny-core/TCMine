using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     O que roda depois de confirmar que o launcher está pareado e online:
///     atualizar-se sozinho se houver uma versão nova, retomar a sessão, e dizer
///     para onde navegar.
///     Um lugar só porque dois chamadores precisavam disso — o arranque
///     (<c>ShellLayout</c>, uma vez por sessão) e o pareamento feito na hora
///     (<c>PairingPage</c>, quando o jogador acabou de digitar o endereço). O
///     segundo chamava só <c>Navigation.NavigateTo("/")</c> depois de parear, sem
///     retomar sessão nenhuma — o jogador saía de <c>/pair</c> direto para o
///     catálogo sem login, e a tela seguinte quebrava por falta de permissão.
/// </summary>
public sealed class PostPairingRoute(UpdateLauncher updater, SignIn account, LauncherShellState shell)
{
    /// <summary>
    ///     A rota para onde navegar, ou <c>null</c> quando o launcher vai
    ///     reiniciar para se atualizar — nesse caso não há para onde navegar,
    ///     porque o processo não vai continuar de pé.
    /// </summary>
    public async Task<string?> ResolveAsync(PairingState pairing, CancellationToken ct)
    {
        if (await updater.HandleAsync(pairing.Server, ct))
            return null;

        shell.Apply(await account.ResumeAsync(pairing.Config!, ct));

        return shell.IsSignedIn ? "/" : "/login";
    }
}
