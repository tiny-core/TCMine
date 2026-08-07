using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Pages.Account;

public partial class ResetPassword : ComponentBase
{
    [SupplyParameterFromQuery(Name = "token")]
    private string? Token { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }
}
