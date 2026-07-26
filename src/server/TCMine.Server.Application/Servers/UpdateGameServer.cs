using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

public sealed class UpdateGameServer(IServerRepository servers)
{
    public async Task<Result> HandleAsync(
        Guid id, string name, string connectAddress, int memoryMb, int maxPlayers, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("Informe o nome do servidor.");

        var server = await servers.GetByIdAsync(id, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        server.Name = name.Trim();
        server.ConnectAddress = connectAddress.Trim();
        server.MemoryMb = memoryMb;
        server.MaxPlayers = maxPlayers;
        server.UpdatedAt = DateTimeOffset.UtcNow;

        await servers.UpdateAsync(server, ct);
        return Result.Success();
    }
}