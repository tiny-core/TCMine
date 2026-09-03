using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>
///     Canal falso, escrito à mão.
///     Fica numa base compartilhada pelo motivo que o CLAUDE.md dá: quando um
///     membro novo aparecer em <see cref="IServerConnection" />, só este arquivo
///     precisa mudar — e não cada suíte que por acaso usa a porta.
/// </summary>
public class FakeServerConnection : IServerConnection
{
    public IReadOnlyList<ModpackDto> Modpacks { get; set; } = [];

    public IReadOnlyList<GameServerDto> Servers { get; set; } = [];

    /// <summary>Lançada nas consultas, para exercitar o caminho de falha.</summary>
    public Exception? Throws { get; set; }

    public List<Uri> Connected { get; } = [];

    public bool Disconnected { get; private set; }

    public bool IsConnected { get; set; }

    public event Action? StateChanged;

    public Task ConnectAsync(Uri serverUrl, CancellationToken ct)
    {
        Connected.Add(serverUrl);
        IsConnected = true;
        StateChanged?.Invoke();

        return Task.CompletedTask;
    }

    public virtual Task DisconnectAsync()
    {
        Disconnected = true;
        IsConnected = false;
        StateChanged?.Invoke();

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModpackDto>> GetModpacksAsync(CancellationToken ct) =>
        Throws is null ? Task.FromResult(Modpacks) : Task.FromException<IReadOnlyList<ModpackDto>>(Throws);

    public Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken ct) =>
        Throws is null ? Task.FromResult(Servers) : Task.FromException<IReadOnlyList<GameServerDto>>(Throws);

    public ValueTask DisposeAsync()
    {
        // CA1816: a classe é herdável (o teste de ordem de saída deriva dela), e
        // sem isto um tipo derivado com finalizador teria de reimplementar tudo.
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}
