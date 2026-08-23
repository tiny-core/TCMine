using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Shared;

public partial class ActiveJobsBar : ComponentBase, IDisposable
{
    private List<KeyValuePair<Guid, JobProgress>> _jobs = [];

    /// <summary>Trabalhos com cancelamento já pedido, para não pedir duas vezes.</summary>
    private readonly HashSet<Guid> _cancelling = [];

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

    /// <summary>
    ///     Pede o cancelamento e marca a linha como "cancelando".
    ///     O trabalho não para no instante do clique: ele para no próximo ponto
    ///     em que verifica o token — no meio de um download de 40 MB, isso pode
    ///     levar segundos. Sem este estado o botão pareceria não ter funcionado,
    ///     e o admin clicaria de novo.
    /// </summary>
    private void Cancel(Guid scopeId)
    {
        _cancelling.Add(scopeId);
        Registry.Cancel(scopeId);
    }

    private void OnChanged()
    {
        _jobs = [.. Registry.Active];

        // Um trabalho que saiu da lista terminou — cancelado ou não. Sem esta
        // limpeza, o mesmo escopo reaparecendo depois nasceria "cancelando".
        _cancelling.RemoveWhere(id => _jobs.All(j => j.Key != id));

        // O evento vem de uma thread do worker; a renderização precisa voltar
        // para o dispatcher do circuito.
        _ = InvokeAsync(StateHasChanged);
    }
}
