using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

public sealed class UpdateGameServer(
    IServerRepository servers,
    IServerWhitelistSync whitelist,
    ICurrentUserScope scope,
    IGamePortAllocator ports)
{
    public async Task<Result> HandleAsync(
        Guid id, string name, string connectAddress, int memoryMb, int maxPlayers,
        bool whitelistEnabled, CancellationToken ct, int gamePort = 0, string? subdomain = null)
    {
        // Antes da validação do nome: responder "informe o nome" a quem nem
        // deveria enxergar este servidor já confirma que ele existe.
        var auth = await scope.RequireAsync(id, ServerAccessPolicy.CanConfigure, ct);
        if (!auth.Succeeded)
            return auth;

        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("Informe o nome do servidor.");

        var server = await servers.GetByIdAsync(id, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        // Nulo = não mexe no subdomínio; vazio = tira. Como a porta abaixo, a
        // recusa vem antes de qualquer campo ser alterado.
        var label = GameDns.NormalizeLabel(subdomain);
        if (label is not null && label != server.Subdomain)
        {
            if (!GameDns.IsValidLabel(label))
                return Result.Fail(GameDns.InvalidLabelMessage);

            if (GameDns.OwnerOf(label, await servers.ListAllAsync(ct), id) is { } taken)
                return Result.Fail($"O subdomínio \"{label}\" já é usado pelo servidor \"{taken.Name}\".");
        }

        // Zero = não mexe na porta. Só confere quando ela MUDA: reconferir a
        // própria porta a cada salvamento seria uma ida ao Docker por um ajuste
        // de nome. A troca chega ao container no próximo start (a porta entra
        // na impressão digital da spec), nunca debaixo de quem está jogando.
        if (gamePort != 0 && gamePort != server.GamePort)
        {
            var available = await ports.EnsureAvailableAsync(gamePort, id, ct);
            if (!available.Succeeded)
                return available;
            server.GamePort = gamePort;
        }

        server.Name = name.Trim();
        server.ConnectAddress = connectAddress.Trim();

        if (subdomain is not null)
            server.Subdomain = label;

        server.MemoryMb = memoryMb;
        server.MaxPlayers = maxPlayers;

        var whitelistMudou = server.WhitelistEnabled != whitelistEnabled;
        server.WhitelistEnabled = whitelistEnabled;
        server.UpdatedAt = DateTimeOffset.UtcNow;

        await servers.UpdateAsync(server, ct);

        // Só quando muda: aplicar a cada salvamento reescreveria a lista inteira
        // por causa de um ajuste de RAM.
        if (whitelistMudou)
            await whitelist.HandleAsync(id, ct);

        return Result.Success();
    }
}
