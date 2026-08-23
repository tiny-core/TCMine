using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Acompanhamento não é regra de negócio: guarda o que foi reportado para
///     quem quiser afirmar sobre isso, e não atrapalha quem não liga.
/// </summary>
public sealed class FakeJobProgress : IJobProgressReporter
{
    public List<JobProgress> Reported { get; } = [];
    public List<(Guid ScopeId, string? Error)> Completed { get; } = [];

    public void Report(Guid scopeId, JobProgress progress) => Reported.Add(progress);

    /// <summary>
    ///     Disparado no momento exato da conclusão. Serve para o teste observar o
    ///     que já estava gravado quando o mundo foi avisado — é assim que se
    ///     trava a ordem "grava, depois anuncia".
    /// </summary>
    public Action? OnComplete { get; set; }

    public void Complete(Guid scopeId, string? error = null)
    {
        Completed.Add((scopeId, error));
        OnComplete?.Invoke();
    }

    /// <summary>Escopos que o teste quer fingir em curso.</summary>
    public HashSet<Guid> EmCurso { get; } = [];

    public bool IsRunning(Guid scopeId) => EmCurso.Contains(scopeId);
}
