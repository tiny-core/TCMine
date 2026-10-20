using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Shared;

/// <summary>
///     A linha embaixo de uma lista paginada por cursor: quantas linhas já
///     vieram e o "Carregar mais", com o indicador enquanto busca. Sem
///     "página N de M" de propósito (ver <c>CursorPage</c>): o histórico só
///     cresce, e contar o total custaria uma varredura a cada página.
///     Antes eram cópias à mão em cada painel da nuvem, cada uma com uma
///     pequena diferença (uma mostrava o indicador, a outra não).
/// </summary>
public partial class LoadMoreBar : ComponentBase
{
    /// <summary>O que já está na tela, ex.: "12 incidentes carregados".</summary>
    [Parameter] [EditorRequired] public string Summary { get; set; } = "";

    /// <summary>Há página seguinte (o cursor não é nulo).</summary>
    [Parameter] public bool HasMore { get; set; }

    [Parameter] public bool Busy { get; set; }

    [Parameter] public EventCallback OnLoadMore { get; set; }

    [Parameter] public string Class { get; set; } = "mt-1";
}
