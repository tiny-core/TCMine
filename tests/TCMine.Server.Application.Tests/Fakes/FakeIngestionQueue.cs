using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Fila de ingestão dos testes. Desde a fase 6.5 a fila leva só a versão, e o
///     worker monta o trabalho a partir das pendências Queued. Este fake faz o
///     mesmo no instante do enfileirar: <see cref="Enfileirados" /> é o que o
///     worker ingeriria — e por isso prova que o agendador GRAVOU o pedido antes
///     de avisar, que é o que torna a fila recuperável.
///     <paramref name="versionOf" /> acha a versão (a mesma instância que o
///     repositório fake guarda).
/// </summary>
public sealed class FakeIngestionQueue(Func<Guid, ModpackVersion?> versionOf) : IIngestionQueue
{
    public List<Guid> Versoes { get; } = [];

    public List<ModIngestionItem> Enfileirados { get; } = [];

    public ValueTask EnqueueAsync(Guid versionId, CancellationToken ct)
    {
        Versoes.Add(versionId);
        if (versionOf(versionId) is { } version)
            Enfileirados.AddRange(IngestionWorkPlanner.Queued(version));
        return ValueTask.CompletedTask;
    }
}
