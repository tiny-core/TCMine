using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Pages.Account;

public partial class Setup : ComponentBase
{
    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }
}
