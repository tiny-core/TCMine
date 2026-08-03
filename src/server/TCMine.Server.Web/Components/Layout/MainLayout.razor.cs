using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TCMine.Server.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    private bool _drawerOpen = true;
    private bool _isDarkMode = true;

    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // localStorage só existe no cliente; lê a preferência salva no primeiro
        // render. Sem valor salvo, mantém o padrão (dark). Como a navegação com
        // forceLoad recria o circuito, é aqui que a escolha do usuário sobrevive.
        if (!firstRender)
            return;

        var stored = await JsRuntime.InvokeAsync<string?>("localStorage.getItem", "tc-dark-mode");
        if (stored is "true" or "false")
        {
            var dark = stored == "true";
            if (dark != _isDarkMode)
            {
                _isDarkMode = dark;
                StateHasChanged();
            }
        }
    }

    private async Task ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
        await JsRuntime.InvokeVoidAsync("localStorage.setItem", "tc-dark-mode", _isDarkMode ? "true" : "false");
    }
}
