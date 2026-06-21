using Microsoft.AspNetCore.Components;
using TCMine_Infrastructure.Server;

namespace TCMine_Server.Components.Layout;

public partial class AdminLayout : LayoutComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    // Settings do servidor — usado para avisar quando os secrets ainda não foram configurados
    [Inject] private ServerSettingsService Settings { get; set; } = null!;

    // Drawer aberto por padrão; o botão de menu alterna (responsivo em telas estreitas)
    private bool _drawerOpen = true;

    // Lista legível dos secrets em falta (vazia = tudo configurado)
    private string? _missingSecrets;

    protected override async Task OnInitializedAsync()
    {
        var stored = await Settings.GetStoredAsync();

        // Monta o aviso conforme o que falta (CF token e/ou Azure client id)
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(stored.CfApiKey)) missing.Add("token do CurseForge");
        if (string.IsNullOrWhiteSpace(stored.AzureClientId)) missing.Add("Azure client id");

        _missingSecrets = missing.Count > 0 ? string.Join(" e ", missing) : null;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    // Logout limpa o cookie no endpoint /auth/logout — reload completo para reiniciar o circuito
    private void Logout()
    {
        Navigation.NavigateTo("/auth/logout", true);
    }
}