using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
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
    ///     Há server pack do autor para puxar os que faltam. Só em rascunho:
    ///     acrescentar arquivo a uma versão publicada é o que a imutabilidade
    ///     impede.
    /// </summary>
    [Parameter] public bool CanUseServerPack { get; set; }

    [Parameter] public bool IsCompleting { get; set; }
    [Parameter] public EventCallback OnCompleteFromServerPack { get; set; }

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

    /// <summary>
    ///     O que é o arquivo, a partir da pasta a que ele pertence.
    ///     "Sem versão compatível" para um shaderpack e para um mod pedem coisas
    ///     diferentes de quem lê: um shaderpack faltando é cosmético, um mod
    ///     faltando pode impedir o jogador de entrar.
    /// </summary>
    private static string TipoFor(string folder) => InstanceFolders.Label(folder);

    /// <summary>
    ///     Onde o arquivo faz falta. É o que diz se a ausência afeta o servidor,
    ///     o jogador, ou os dois.
    /// </summary>
    private static string LadoFor(FileSide side) => side switch
    {
        FileSide.ClientOnly => "só cliente",
        FileSide.ServerOnly => "só servidor",
        _ => "cliente e servidor"
    };

    private static string LabelFor(PendingModReason reason) => reason switch
    {
        PendingModReason.DistributionDenied => "Sem redistribuição",
        PendingModReason.NoCompatibleFile => "Sem versão compatível",
        _ => "Falha temporária"
    };
}
