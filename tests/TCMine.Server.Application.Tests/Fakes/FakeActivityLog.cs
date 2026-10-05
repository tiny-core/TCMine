using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>Feed de atividade em memória, para os testes que afirmam o que foi gravado.</summary>
internal sealed class FakeActivityLog : IActivityLogRepository
{
    public List<ActivityEvent> Entries { get; } = [];

    public Task AddAsync(ActivityEvent entry, CancellationToken ct)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivityEvent>> ListRecentAsync(int count, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ActivityEvent>>([.. Entries.OrderByDescending(e => e.Id).Take(count)]);
}
