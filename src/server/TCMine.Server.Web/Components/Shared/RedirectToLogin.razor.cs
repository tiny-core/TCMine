using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Shared;

/// <summary>
///     Manda um visitante sem sessão para o login, preservando para onde ele
///     queria ir (returnUrl) — depois de entrar, volta ao destino original.
/// </summary>
public sealed class RedirectToLogin : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        var returnUrl = Uri.EscapeDataString(
            Navigation.ToBaseRelativePath(Navigation.Uri));

        // forceLoad: o login é SSR estático (precisa de HttpContext para o
        // cookie), então tem de sair do circuito interativo.
        Navigation.NavigateTo($"/login?returnUrl={returnUrl}", true);
    }
}
