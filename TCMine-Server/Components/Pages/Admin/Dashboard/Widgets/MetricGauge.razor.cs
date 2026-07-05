using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

/// <summary>
///     Medidor circular reutilizável para uma métrica de uso (CPU, RAM, disco). Recebe a percentagem
///     (0–100) e decide a cor por limiar; o rótulo e a legenda (ex.: "12 / 32 GB") vêm de fora.
/// </summary>
public partial class MetricGauge : ComponentBase
{
    // Percentagem de uso (0–100) desenhada no anel
    [Parameter] [EditorRequired] public double Value { get; set; }

    // Rótulo curto sob o anel (ex.: "CPU", "RAM", "Disco")
    [Parameter] [EditorRequired] public string Label { get; set; } = null!;

    // Ícone do Material ao lado do rótulo
    [Parameter] [EditorRequired] public string Icon { get; set; } = null!;

    // Legenda opcional abaixo (ex.: valores absolutos "12,3 / 32,0 GB")
    [Parameter] public string? Caption { get; set; }

    // Cor por limiar: verde até 70%, atenção até 90%, crítico acima disso
    private Color GaugeColor => Value switch
    {
        >= 90 => Color.Error,
        >= 70 => Color.Warning,
        _ => Color.Success
    };
}