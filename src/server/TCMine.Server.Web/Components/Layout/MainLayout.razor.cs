using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TCMine.Server.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IAsyncDisposable
{
    private bool _drawerOpen = true;
    private IJSObjectReference? _module;

    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

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
    }

    private async Task LogoutAsync()
    {
        if (_module is not null)
            await _module.InvokeVoidAsync("submitForm", "tc-logout-form");
    }

}
