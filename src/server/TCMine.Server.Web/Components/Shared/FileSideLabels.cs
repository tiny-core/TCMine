using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Web.Components.Shared;

/// <summary>
///     Rótulo em português do lado de um arquivo, num lugar só. Era repetido
///     (um switch igual em <c>PendingModsPanel</c>, e nenhum na célula "Lado"
///     de <c>ModpackModsPage</c> fora do rascunho — ali saía o nome bruto do
///     enum, "Both"/"ClientOnly"/"ServerOnly").
/// </summary>
public static class FileSideLabels
{
    public static string Label(FileSide side) => side switch
    {
        FileSide.ClientOnly => "só cliente",
        FileSide.ServerOnly => "só servidor",
        _ => "cliente e servidor"
    };
}
