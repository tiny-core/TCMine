using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Pages.Account;

public partial class ForgotPassword : ComponentBase
{
    /// <summary>Confirmação genérica, devolvida pelo endpoint via query string.</summary>
    [SupplyParameterFromQuery(Name = "sent")]
    private bool Sent { get; set; }

    private string? Info => Sent
        ? "Se o e-mail estiver cadastrado, o link de recuperação já foi enviado. Ele vale por 1 hora."
        : null;
}
