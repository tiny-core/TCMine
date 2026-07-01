using Microsoft.AspNetCore.Components;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Layout;

public partial class AdminLayout : LayoutComponentBase, IDisposable
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
        // Reage a gravações nas settings para o aviso somir/atualizar sem reload da página
        Settings.Changed += OnSettingsChanged;

        Refresh(await Settings.GetStoredAsync());
    }

    // Recalcula o aviso a partir do snapshot atual das settings
    private void Refresh(ServerSettings stored)
    {
        // Monta o aviso conforme o que falta (CF token e/ou Azure client id)
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(stored.CfApiKey)) missing.Add("token do CurseForge");
        if (string.IsNullOrWhiteSpace(stored.AzureClientId)) missing.Add("Azure client id");

        _missingSecrets = missing.Count > 0 ? string.Join(" e ", missing) : null;
    }

    // O evento vem da thread que gravou; volta ao contexto do renderer antes de re-renderizar
    private void OnSettingsChanged(ServerSettings stored)
    {
        InvokeAsync(() =>
        {
            Refresh(stored);
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        // Singleton de longa vida: sem unsubscribe o componente vazaria a cada navegação
        Settings.Changed -= OnSettingsChanged;
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