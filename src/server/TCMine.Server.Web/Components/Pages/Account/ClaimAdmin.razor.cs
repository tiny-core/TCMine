using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Pages.Account;

public partial class ClaimAdmin : ComponentBase
{
    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }
}
