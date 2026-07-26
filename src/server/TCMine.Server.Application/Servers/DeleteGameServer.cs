using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

public sealed class DeleteGameServer(IServerRepository servers)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken ct)
    {
        // Fatia 1: só remove o registro. Com container e mundo (fatias 3/3.5),
        // apagar terá de parar/remover o container e decidir sobre o mundo —
        // por ora não há nem um, nem outro.
        await servers.RemoveAsync(id, ct);
        return Result.Success();
    }
}