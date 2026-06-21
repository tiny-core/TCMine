using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using TCMine_Data.Authentication;

namespace TCMine_Server.Components.Pages;

/// <summary>
/// Login do painel — SSR estático. Valida usuário+senha no banco e, em caso de sucesso,
/// emite o cookie de autenticação (<c>SignInAsync</c>) e redireciona para o painel.
/// </summary>
public partial class Login : ComponentBase
{
    [Inject] private UserService Users { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    // HttpContext cascateia em SSR estático — necessário para SignInAsync
    [CascadingParameter] private HttpContext HttpContext { get; set; } = null!;

    // Dados do formulário, preenchidos a partir do POST
    [SupplyParameterFromForm] public required LoginInput Input { get; set; }

    // Marca falha de credenciais para reexibir o formulário com alerta (sem redirect)
    private bool _failed;

    private async Task LoginUser()
    {
        var user = await Users.ValidateCredentialsAsync(Input.Username, Input.Password);
        if (user is null)
        {
            // Form válido, mas credenciais erradas — re-renderiza com o alerta
            _failed = true;
            return;
        }

        // Emite o cookie persistente e navega para o painel (NavigateTo em SSR vira redirect)
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaims.BuildPrincipal(user),
            new AuthenticationProperties { IsPersistent = true });

        Navigation.NavigateTo("/admin");
    }

    // Modelo isolado do formulário
    public sealed class LoginInput
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Regras de validação do formulário de login, usando FluentValidation em vez de
    /// DataAnnotations. Registado em DI (<c>AddValidatorsFromAssemblyContaining&lt;LoginInputValidator&gt;()</c>)
    /// para que o <c>FluentValidationValidator</c> (Brazilla.FluentValidation) o resolva automaticamente.
    /// </summary>
    public sealed class InputValidator : AbstractValidator<LoginInput>
    {
        public InputValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Usuário é necessário");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Senha é necessária");
        }
    }
}