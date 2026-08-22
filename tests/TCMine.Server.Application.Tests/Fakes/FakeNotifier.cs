using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Notificador de hub em memória. Guarda o que foi empurrado para os testes
///     que precisam afirmar sobre isso — no caso do papel, afirmar que o aviso
///     SAIU é o teste: sem ele, quem perdeu o acesso continua com o console
///     aberto até resolver reconectar.
/// </summary>
internal sealed class FakeNotifier : IServerHubNotifier
{
    public List<(Guid ServerId, Guid UserId, ServerRoleDto? Role)> PapeisAvisados { get; } = [];

    public Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task NotifyConsoleLineAsync(Guid serverId, ConsoleLineDto line, CancellationToken ct) =>
        Task.CompletedTask;

    public Task NotifyPlayerCountChangedAsync(Guid serverId, int online, int max, CancellationToken ct) =>
        Task.CompletedTask;

    public Task NotifyRoleChangedAsync(Guid serverId, Guid userId, ServerRoleDto? role, CancellationToken ct)
    {
        PapeisAvisados.Add((serverId, userId, role));
        return Task.CompletedTask;
    }
}
