using Microsoft.AspNetCore.Components;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Shared;

/// <summary>
/// Overlay bloqueante global de feedback async. Renderizado uma única vez no <c>RootLayout</c>,
/// reage ao <see cref="BusyService"/> do circuito: aparece enquanto há operação em andamento e some
/// quando a última termina. Não-fechável de propósito (ver markup no .razor).
/// </summary>
public partial class BusyOverlay : ComponentBase, IDisposable
{
    [Inject] private BusyService Busy { get; set; } = null!;

    protected override void OnInitialized()
    {
        Busy.OnChange += OnBusyChanged;
    }

    // O estado muda fora do ciclo de render do componente; reagenda na sync context do Blazor
    private void OnBusyChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Busy.OnChange -= OnBusyChanged;
    }
}