using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class LoginPage : ComponentBase, IDisposable
{
    private bool _busy;

    [Inject] private SignIn Account { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    ///     A mensagem vem do estado da moldura, e não de um campo local, porque a
    ///     tentativa que mais importa não acontece aqui: é a do arranque, com a
    ///     credencial guardada. Guardando o resultado só no clique, uma sessão
    ///     expirada levava o jogador a um login em branco, sem dizer o que
    ///     aconteceu — ele clicava para descobrir.
    ///     O aviso de ligação NÃO entra aqui: ele já está na moldura, logo acima,
    ///     e repeti-lo punha a mesma frase duas vezes na tela.
    /// </summary>
    private string? Message => _busy ? null : Shell.Account?.Message;

    /// <summary>
    ///     Conta recusada é aviso, não erro: repetir com a mesma conta dá no
    ///     mesmo, e o vermelho convidaria a tentar de novo.
    /// </summary>
    private Severity Severity =>
        Shell.Account?.Status is SignInStatus.Rejected ? Severity.Warning : Severity.Error;

    public void Dispose()
    {
        Shell.Changed -= OnShellChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Shell.Changed += OnShellChanged;

    private void OnShellChanged() => InvokeAsync(StateHasChanged);

    private async Task SignInAsync()
    {
        if (Shell.Pairing?.Config is not { } config)
            return;

        _busy = true;

        try
        {
            var estado = await Account.InteractiveAsync(config, CancellationToken.None);

            Shell.Apply(estado);

            if (estado.IsSignedIn)
                Navigation.NavigateTo("/");
        }
        finally
        {
            _busy = false;
        }
    }
}
