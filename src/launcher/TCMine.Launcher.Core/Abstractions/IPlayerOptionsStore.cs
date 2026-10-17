using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     O <c>options.txt</c> de uma instância, e o que se sabe da última vez que
///     ele foi sincronizado com a conta.
///     Porta e não <c>System.IO</c> no caso de uso, pela mesma regra do
///     <see cref="IInstanceStore" />: a decisão de quem vence (este PC ou a
///     conta) tem de ser testável sem disco.
/// </summary>
public interface IPlayerOptionsStore
{
    /// <summary>O arquivo, ou nulo se o jogo ainda não o criou nesta instância.</summary>
    Task<LocalOptions?> ReadAsync(InstanceKey key, CancellationToken ct);

    Task WriteAsync(InstanceKey key, string text, CancellationToken ct);

    Task<OptionsSyncState?> ReadStateAsync(InstanceKey key, CancellationToken ct);

    Task WriteStateAsync(InstanceKey key, OptionsSyncState state, CancellationToken ct);
}

public sealed record LocalOptions(string Text, DateTimeOffset LastWriteAt);

/// <summary>
///     A última sincronização desta instância: a impressão digital do que ficou
///     em comum com a conta, e a hora (do servidor) dessa gravação. É o que
///     separa "mudou aqui" de "mudou noutro PC".
/// </summary>
public sealed record OptionsSyncState(string Fingerprint, DateTimeOffset RemoteSavedAt);
