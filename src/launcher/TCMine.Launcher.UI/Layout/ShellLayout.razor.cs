using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class ShellLayout : LayoutComponentBase, IDisposable
{
    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private ServerPairing Pairing { get; set; } = default!;

    [Inject] private SignIn Account { get; set; } = default!;

    [Inject] private UpdateLauncher Updater { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     O arranque mora no layout, e não numa página, porque ele acontece uma
    ///     vez por sessão: o layout não é reconstruído ao navegar, e a checagem
    ///     não pode repetir a cada troca de tela.
    ///     São dois passos em ordem, e a ordem importa: sem servidor não há a
    ///     quem pedir sessão, e o client id do Azure — que a autenticação exige —
    ///     vem justamente da configuração que o pareamento gravou.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        Shell.Changed += OnShellChanged;
        Shell.BeginCheck();

        try
        {
            var pairing = await Pairing.ResumeAsync(CancellationToken.None);
            Shell.Apply(pairing);

            if (!pairing.IsPaired)
            {
                Navigation.NavigateTo("/pair");
                return;
            }

            // Servidor fora do ar não tem como emitir sessão — e mandar para o
            // login nesse caso prendia o jogador: a tela pede a conta Microsoft,
            // o login precisa do servidor para trocar a prova por sessão, e o
            // servidor é justamente o que não está lá. Sem saída, com o disco
            // cheio de coisa instalada do outro lado.
            // Offline fica-se onde se está. O que precisa de rede aparece
            // desligado, com o motivo, em vez de levar a lado nenhum.
            if (!pairing.IsOnline)
                return;

            // Antes do login, e de propósito: uma atualização reinicia a
            // aplicação, e fazê-la depois de o jogador entrar desperdiçaria o
            // login que ele acabou de fazer. Devolve verdadeiro só quando vai
            // mesmo reiniciar — daí parar aqui.
            if (await Updater.HandleAsync(pairing.Server, CancellationToken.None))
                return;

            Shell.Apply(await Account.ResumeAsync(pairing.Config!, CancellationToken.None));

            if (!Shell.IsSignedIn)
                Navigation.NavigateTo("/login");
        }
        finally
        {
            // No finally: uma exceção aqui deixaria a janela girando para sempre,
            // que é o pior desfecho possível para um arranque.
            Shell.FinishStartup();
        }
    }

    private void OnShellChanged() => InvokeAsync(StateHasChanged);
}
