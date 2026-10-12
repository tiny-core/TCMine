using System.Runtime.CompilerServices;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Docker;

/// <summary>
///     <see cref="IContainerEvents" /> sobre o stream /events do Docker.
/// </summary>
public sealed class DockerContainerEvents(DockerApiClient docker) : IContainerEvents
{
    /// <summary>
    ///     O label que o orquestrador põe em todo container de jogo (ver
    ///     DockerServerOrchestrator). É por ele que o evento chega ao servidor.
    /// </summary>
    public const string ServerLabel = "tcmine.server";

    /// <summary>
    ///     As ações que podem mudar o estado. O que fica de fora pesa: o
    ///     <c>exec_*</c> dispara a cada comando RCON — o coletor de métricas faz
    ///     um a cada quinze segundos por servidor —, e tratá-lo seria consultar o
    ///     Docker sem parar para descobrir que nada mudou.
    /// </summary>
    private static readonly HashSet<string> StateActions =
    [
        "create", "start", "restart", "die", "stop", "kill", "oom", "pause", "unpause", "destroy"
    ];

    public async IAsyncEnumerable<Guid> WatchAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var e in docker.StreamEventsAsync(ct))
        {
            if (ServerOf(e.Action, e.Actor.Attributes) is { } serverId)
                yield return serverId;
        }
    }

    /// <summary>
    ///     O servidor de um evento, ou nulo quando ele não interessa: container
    ///     que não é nosso, ou ação que não muda o estado.
    ///     As mudanças de saúde chegam como "health_status: healthy" — a ação
    ///     traz o resultado colado —, por isso a comparação é pelo começo.
    /// </summary>
    public static Guid? ServerOf(string action, IReadOnlyDictionary<string, string>? attributes)
    {
        var relevante = StateActions.Contains(action)
                        || action.StartsWith("health_status", StringComparison.Ordinal);

        if (!relevante || attributes is null)
            return null;

        return attributes.TryGetValue(ServerLabel, out var value) && Guid.TryParse(value, out var id)
            ? id
            : null;
    }
}
