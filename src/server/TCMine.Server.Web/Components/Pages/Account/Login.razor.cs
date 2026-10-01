using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Components.Pages.Account;

public partial class Login : ComponentBase
{
    /// <summary>Mensagem de falha, devolvida pelo endpoint via query string.</summary>
    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }

    /// <summary>Para onde voltar depois de entrar.</summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrl { get; set; }

    [Inject] private ISettingsRepository Settings { get; set; } = default!;
    [Inject] private IOptions<ServerOptions> ServerOptions { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string StartUrl => string.IsNullOrWhiteSpace(ReturnUrl)
        ? "/auth/microsoft/start"
        : $"/auth/microsoft/start?returnUrl={Uri.EscapeDataString(ReturnUrl)}";

    protected override async Task OnInitializedAsync()
    {
        // Sem Client ID não há para onde mandar o "Entrar com a Microsoft" —
        // manda configurar primeiro. NÃO é "sem usuário ainda": salvar o
        // Client ID no /setup não cria ninguém, só o primeiro login de
        // verdade cria (e vira admin) — checar por usuário aqui faria esta
        // tela devolver para o /setup outra vez, sempre, porque ninguém chega
        // a clicar no botão.
        var clientId = await AzureClientIdResolver.ResolveAsync(
            Settings, ServerOptions.Value.AzureClientId, CancellationToken.None);

        if (string.IsNullOrWhiteSpace(clientId))
            Navigation.NavigateTo("/admin/setup", true);
    }
}
