using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Shared;

public partial class ActiveJobsBar : ComponentBase, IDisposable
{
    private List<KeyValuePair<Guid, JobProgress>> _jobs = [];

    [Inject] private JobProgressRegistry Registry { get; set; } = default!;

    public void Dispose()
    {
        Registry.Changed -= OnChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        _jobs = [.. Registry.Active];

        // Empurrão, não sondagem: o worker avisa e a barra se redesenha. Sondar
        // o banco a cada 2s dava um progresso atrasado e grosseiro, e ainda
        // custava uma consulta por circuito aberto.
        Registry.Changed += OnChanged;
    }

    private void OnChanged()
    {
        _jobs = [.. Registry.Active];

        // O evento vem de uma thread do worker; a renderização precisa voltar
        // para o dispatcher do circuito.
        _ = InvokeAsync(StateHasChanged);
    }
}
