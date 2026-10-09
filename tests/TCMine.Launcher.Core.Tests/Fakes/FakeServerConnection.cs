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

    /// <summary>Manifestos por id de versão, para o instalador.</summary>
    public Dictionary<Guid, ModpackVersionDto> Versions { get; } = [];

    /// <summary>Versão mais recente por (modpack, canal).</summary>
    public Dictionary<(Guid, ReleaseChannel), ModpackVersionDto> Latest { get; } = [];

    /// <summary>
    ///     Que (modpack, canal) foram consultados, na ordem. Serve para provar
    ///     que quem chama não pergunta a mesma coisa duas vezes — e que um canal
    ///     nunca é consultado no lugar do outro.
    /// </summary>
    public List<(Guid Modpack, ReleaseChannel Channel)> LatestQueries { get; } = [];

    /// <summary>Novidades por modpack.</summary>
    public Dictionary<Guid, IReadOnlyList<ModpackNewsDto>> News { get; } = [];

    /// <summary>Histórico por (modpack, canal), para o seletor de versão.</summary>
    public Dictionary<(Guid, ReleaseChannel), IReadOnlyList<ModpackVersionSummaryDto>> Histories { get; } = [];

    /// <summary>Servidores pedidos, na ordem — para provar que o id certo foi enviado.</summary>
    public List<Guid> AccessRequested { get; } = [];

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

    public Task<IReadOnlyList<ModpackNewsDto>> GetNewsAsync(Guid modpackId, CancellationToken ct) =>
        Throws is not null
            ? Task.FromException<IReadOnlyList<ModpackNewsDto>>(Throws)
            : Task.FromResult(News.GetValueOrDefault(modpackId, []));

    public Task<IReadOnlyList<ModpackVersionSummaryDto>> GetVersionsAsync(
        Guid modpackId, ReleaseChannel channel, CancellationToken ct) =>
        Throws is not null
            ? Task.FromException<IReadOnlyList<ModpackVersionSummaryDto>>(Throws)
            : Task.FromResult(Histories.GetValueOrDefault((modpackId, channel), []));

    public Task<ModpackVersionDto?> GetLatestVersionAsync(
        Guid modpackId, ReleaseChannel channel, CancellationToken ct)
    {
        LatestQueries.Add((modpackId, channel));

        // Honra o Throws como os outros métodos: sem isto, um teste de canal em
        // baixo passaria por este caminho como se tudo estivesse bem.
        return Throws is not null
            ? Task.FromException<ModpackVersionDto?>(Throws)
            : Task.FromResult(Latest.GetValueOrDefault((modpackId, channel)));
    }

    public Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId, CancellationToken ct) =>
        Throws is not null
            ? Task.FromException<ModpackVersionDto>(Throws)
            : Versions.TryGetValue(versionId, out var v)
                ? Task.FromResult(v)
                : Task.FromException<ModpackVersionDto>(new InvalidOperationException("Versão não encontrada."));

    public Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken ct) =>
        Throws is null ? Task.FromResult(Servers) : Task.FromException<IReadOnlyList<GameServerDto>>(Throws);

    public Task RequestServerAccessAsync(Guid gameServerId, CancellationToken ct)
    {
        AccessRequested.Add(gameServerId);
        return Throws is null ? Task.CompletedTask : Task.FromException(Throws);
    }

    public ValueTask DisposeAsync()
    {
        // CA1816: a classe é herdável (o teste de ordem de saída deriva dela), e
        // sem isto um tipo derivado com finalizador teria de reimplementar tudo.
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}
