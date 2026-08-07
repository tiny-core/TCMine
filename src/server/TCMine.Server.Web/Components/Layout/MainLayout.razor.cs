using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Server.Web.Components.Features.Account;

namespace TCMine.Server.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IAsyncDisposable
{
    private bool _drawerOpen = true;
    private bool _isDarkMode = true;
    private IJSObjectReference? _module;

    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            // O circuito pode já ter caído (navegação com forceLoad, aba fechada);
            // aí a chamada ao JS falha e não há nada a limpar mesmo.
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Ignorado de propósito: sem circuito, o módulo já se foi.
            }
        }

        GC.SuppressFinalize(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _module = await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./Components/Layout/MainLayout.razor.js");

        // localStorage só existe no cliente; lê a preferência salva no primeiro
        // render. Sem valor salvo, mantém o padrão (dark). Como a navegação com
        // forceLoad recria o circuito, é aqui que a escolha do usuário sobrevive.
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

    private async Task OpenChangePassword()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        await DialogService.ShowAsync<ChangePasswordDialog>("Alterar senha", options);
    }

    private async Task LogoutAsync()
    {
        if (_module is not null)
            await _module.InvokeVoidAsync("submitForm", "tc-logout-form");
    }

    private async Task ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
        await JsRuntime.InvokeVoidAsync("localStorage.setItem", "tc-dark-mode", _isDarkMode ? "true" : "false");
    }
}
