using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Updates;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Components.Shared;

/// <summary>
///     A versão desta instalação e a mais nova publicada, com "Verificar agora".
///     O aviso no topo do painel responde do cache (até uma hora); esta tela é
///     onde o admin que acabou de publicar confirma sem esperar. Consulta só
///     depois do primeiro render interativo, como o aviso: a pré-renderização
///     não sai para a internet.
/// </summary>
public partial class ServerUpdatePanel : ComponentBase
{
    private bool _checking;
    private bool _loaded;
    private ServerUpdateStatus? _status;

    [Inject] private CheckServerUpdate CheckUpdate { get; set; } = default!;

    private static string CurrentVersion => BuildInfo.Version;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await LoadAsync(refresh: false);
    }

    private Task CheckNowAsync() => LoadAsync(refresh: true);

    private async Task LoadAsync(bool refresh)
    {
        _checking = true;
        StateHasChanged();
        try
        {
            _status = await CheckUpdate.CheckAsync(CurrentVersion, refresh, CancellationToken.None);
        }
        finally
        {
            _checking = false;
            _loaded = true;
            StateHasChanged();
        }
    }

    private static string CheckedAtText(DateTimeOffset at) =>
        $"Última resposta do GitHub: {at.ToLocalTime():dd/MM/yyyy HH:mm}.";
}
