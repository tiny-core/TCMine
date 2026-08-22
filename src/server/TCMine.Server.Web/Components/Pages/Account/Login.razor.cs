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
    [Inject] private ISettingsRepository Settings { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    ///     Só oferece a recuperação quando há como enviar o e-mail.
    ///     Sem SMTP o link leva a um formulário que responde "enviamos, confira
    ///     sua caixa" — a resposta é deliberadamente igual para e-mail existente
    ///     e inexistente, para a tela não virar um verificador de contas — e
    ///     nada chega nunca. A pessoa fica esperando em vez de procurar o admin.
    /// </summary>
    private bool PodeRecuperar { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Instalação nova: não há em quem fazer login ainda, então manda criar o
        // administrador. O /setup se recusa a rodar depois que existe alguém.
        if (!await Users.AnyAsync(CancellationToken.None))
        {
            Navigation.NavigateTo("/setup", true);
            return;
        }

        PodeRecuperar = (await Settings.GetAsync(CancellationToken.None)).HasSmtp;
    }
}
