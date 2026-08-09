using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class PendingModsPanel : ComponentBase
{
    [Parameter] [EditorRequired] public IReadOnlyList<PendingMod> Pending { get; set; } = [];

    /// <summary>Só faz sentido enviar arquivo enquanto a versão é rascunho.</summary>
    [Parameter] public bool CanUpload { get; set; }

    [Parameter] public bool IsBusy { get; set; }

    [Parameter] public EventCallback<PendingMod> OnUpload { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }

    /// <summary>
    ///     Só oferece "tentar de novo" quando há pendência que pode mudar de
    ///     resultado. Redistribuição negada nunca muda — insistir só frustra.
    /// </summary>
    private bool CanRetry => Pending.Any(p => p.Reason is not PendingModReason.DistributionDenied);

    private static Color ColorFor(PendingModReason reason) => reason switch
    {
        PendingModReason.DistributionDenied => Color.Warning,
        PendingModReason.NoCompatibleFile => Color.Error,
        _ => Color.Info
    };

    private static string LabelFor(PendingModReason reason) => reason switch
    {
        PendingModReason.DistributionDenied => "Sem redistribuição",
        PendingModReason.NoCompatibleFile => "Sem versão compatível",
        _ => "Falha temporária"
    };
}
