using System.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace TCMine_Server.Components.Pages;

public partial class Error : ComponentBase
{
    // HttpContext cascade-ado pelo pipeline do ASP.NET — disponível apenas em Blazor Server
    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized()
    {
        // Activity.Current captura o trace ID do OpenTelemetry; fallback para o ID do HttpContext
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
    }
}