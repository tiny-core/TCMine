using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Components.Pages.Account;

public partial class Login : ComponentBase
{
    /// <summary>Mensagem de falha, devolvida pelo endpoint via query string.</summary>
    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }

    /// <summary>Para onde voltar depois de entrar.</summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrl { get; set; }

    [Inject] private IUserRepository Users { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Instalação nova: não há em quem fazer login ainda, então manda criar o
        // administrador. O /setup se recusa a rodar depois que existe alguém.
        if (!await Users.AnyAsync(CancellationToken.None))
            Navigation.NavigateTo("/setup", true);
    }
}
