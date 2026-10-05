using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Updates;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Components.Shared;

/// <summary>
///     Aviso de versão nova do servidor, para o admin da instalação.
///     Consulta DEPOIS do primeiro render interativo, e não no OnInitialized:
///     assim a pré-renderização (e todo GET de teste) não sai para a internet, e
///     uma resposta lenta do GitHub nunca atrasa a página. Fechar vale para a
///     sessão do circuito; na próxima visita, se ainda houver versão nova, volta.
/// </summary>
public partial class ServerUpdateBanner : ComponentBase
{
    private ServerRelease? _release;
    private bool _dismissed;

    [Inject] private CheckServerUpdate CheckUpdate { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _release = await CheckUpdate.HandleAsync(BuildInfo.Version, CancellationToken.None);
        if (_release is not null)
            StateHasChanged();
    }
}
