using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Models;

public class MetricModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public Color Color { get; set; } = Color.Default;
    public string? Icon { get; set; }
    public string? Tooltip { get; set; }
    public string? Subtext { get; set; }
    public RenderFragment? ChildContent { get; set; }
}