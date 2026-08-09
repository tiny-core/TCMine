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

    public void Complete(Guid scopeId, string? error = null) => Completed.Add((scopeId, error));
}
