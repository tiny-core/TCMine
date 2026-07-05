using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using TCMine_Domain.Identity;
using TCMine_Server.Authentication;
using TCMine_Server.Infrastructure.Identity;

namespace TCMine_Server.Components.Pages;

/// <summary>
///     Setup de primeira execução — cria o usuário master (Owner). Após criar, emite o cookie
///     e leva direto à página de Configurações para o Owner cadastrar os secrets (CF/Azure).
/// </summary>
public partial class Setup : ComponentBase
{
    [Inject] private UserService Users { get; set; } = null!;
    [Inject] private SetupState SetupState { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [CascadingParameter] private HttpContext HttpContext { get; set; } = null!;

    // Sem inicializador inline (BL0008): no POST o binder pode sobrescrever com null antes do bind.
    // Inicializa no OnInitialized só quando o form não veio preenchido (GET / primeira renderização).
    [SupplyParameterFromForm] private SetupInput Input { get; set; } = null!;

    protected override void OnInitialized()
    {
        Input ??= new SetupInput();
    }

    private async Task CreateMaster()
    {
        // Proteção extra (corrida/abuso): se já existe usuário, o setup não recria
        if (await Users.AnyUsersExistAsync())
        {
            Navigation.NavigateTo("/login");
            return;
        }

        var user = await Users.CreateAsync(Input.Username, Input.Password, UserRole.Owner);
        SetupState.MarkInitialized();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaims.BuildPrincipal(user),
            new AuthenticationProperties { IsPersistent = true });

        // Próximo passo do fluxo: configurar os secrets
        Navigation.NavigateTo("/admin/settings");
    }

    // Modelo isolado do formulário
    private sealed class SetupInput
    {
        [Required(ErrorMessage = "Usuário é necessário")]
        [MinLength(3, ErrorMessage = "Mínimo de 3 caracteres")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha é necessária")]
        [MinLength(8, ErrorMessage = "Mínimo de 8 caracteres")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme a senha")]
        [Compare(nameof(Password), ErrorMessage = "As senhas não coincidem")]
        public string Confirm { get; set; } = string.Empty;
    }
}