using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>Responde que alcança só os endereços listados, e lembra o que foi sondado.</summary>
public sealed class FakeEndpointProbe(params ServerAddress[] reachable) : IEndpointProbe
{
    private readonly HashSet<ServerAddress> _reachable = [.. reachable];

    public List<ServerAddress> Probed { get; } = [];

    public Task<bool> IsReachableAsync(ServerAddress address, CancellationToken ct)
    {
        Probed.Add(address);
        return Task.FromResult(_reachable.Contains(address));
    }
}
