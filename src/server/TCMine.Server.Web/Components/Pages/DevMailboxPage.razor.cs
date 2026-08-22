using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using TCMine.Server.Web.Background;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Components.Pages;

public partial class DevMailboxPage : ComponentBase
{
    [Inject] private DevMailbox Mailbox { get; set; } = default!;
    [Inject] private IOptions<DevMailOptions> Options { get; set; } = default!;

    private void Limpar()
    {
        Mailbox.Clear();
        StateHasChanged();
    }
}
