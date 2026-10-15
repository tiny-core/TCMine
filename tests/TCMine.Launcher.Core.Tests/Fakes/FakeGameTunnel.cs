using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>Um túnel que abre (ou não) quando mandado, e lembra se foi fechado.</summary>
public sealed class FakeGameTunnel(bool answers = true) : IGameTunnel
{
    public static readonly ServerAddress Local = new("127.0.0.1", 50123);

    public List<string> Opened { get; } = [];
    public FakeSession? Last { get; private set; }

    public Task<IGameTunnelSession?> OpenAsync(string hostname, CancellationToken ct)
    {
        Opened.Add(hostname);

        if (!answers)
            return Task.FromResult<IGameTunnelSession?>(null);

        Last = new FakeSession();
        return Task.FromResult<IGameTunnelSession?>(Last);
    }

    public sealed class FakeSession : IGameTunnelSession
    {
        public int Closes { get; private set; }

        public ServerAddress LocalAddress => Local;

        public ValueTask DisposeAsync()
        {
            Closes++;
            return ValueTask.CompletedTask;
        }
    }
}
