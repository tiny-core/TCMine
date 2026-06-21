using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine_Server.Components.Shared;

public partial class StatCard : ComponentBase
{
    // Ícone do Material exibido à esquerda
    [Parameter] [EditorRequired] public string Icon { get; set; } = null!;

    // Cor do ícone (acento do tema)
    [Parameter] public Color Color { get; set; } = Color.Default;

    // Valor em destaque (número ou texto curto)
    [Parameter] [EditorRequired] public string Value { get; set; } = null!;

    // Rótulo descritivo abaixo do valor
    [Parameter] [EditorRequired] public string Label { get; set; } = null!;

    // Linha secundária opcional (ex.: "3 publicados", "2 rodando") — fica abaixo do rótulo
    [Parameter] public string? Subtitle { get; set; }

    // Converte a Color do MudBlazor no nome do token CSS de cor do tema (var(--mud-palette-*)),
    // usado pela faixa de acento e pelo fundo tingido do ícone no CSS escopado.
    private string PaletteVar => Color switch
    {
        Color.Primary => "primary",
        Color.Secondary => "secondary",
        Color.Tertiary => "tertiary",
        Color.Info => "info",
        Color.Success => "success",
        Color.Warning => "warning",
        Color.Error => "error",
        _ => "primary"
    };
}