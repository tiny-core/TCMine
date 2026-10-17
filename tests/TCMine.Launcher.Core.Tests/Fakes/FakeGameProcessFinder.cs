using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>Devolve o processo dado só para a identidade dada — o resto "já saiu".</summary>
public sealed class FakeGameProcessFinder(GameProcessIdentity? alive = null, IGameProcess? process = null)
    : IGameProcessFinder
{
    public IGameProcess? TryReattach(GameProcessIdentity identity, string instanceDirectory) =>
        identity == alive ? process : null;
}
