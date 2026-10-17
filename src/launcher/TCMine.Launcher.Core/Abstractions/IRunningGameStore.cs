using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     O jogo aberto, gravado no disco enquanto ele corre.
///     É o que deixa um launcher reaberto reencontrar o jogo que o anterior
///     abriu: sem isto, ele acharia que não há jogo nenhum, ofereceria "Jogar" de
///     novo, e duas cópias escreveriam o mesmo mundo.
/// </summary>
public interface IRunningGameStore
{
    Task<RunningGameRecord?> ReadAsync(CancellationToken ct);

    Task WriteAsync(RunningGameRecord record, CancellationToken ct);

    Task ClearAsync(CancellationToken ct);
}

public sealed record RunningGameRecord(InstanceKey Instance, GameProcessIdentity Process);
