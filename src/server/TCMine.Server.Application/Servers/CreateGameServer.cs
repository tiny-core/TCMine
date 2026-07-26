using System.Security.Cryptography;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

public sealed class CreateGameServer(
    IServerRepository servers,
    IModpackRepository modpacks,
    ICurrentUserScope userScope)
{
    public async Task<Result<Guid>> HandleAsync(
        Guid modpackId, string name, string connectAddress, int memoryMb, int maxPlayers, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Fail("Informe o nome do servidor.");
        if (string.IsNullOrWhiteSpace(connectAddress))
            return Result<Guid>.Fail("Informe o endereço de conexão.");

        // Fixa a versão publicada mais recente. Sem versão publicada não há o
        // que rodar — o servidor precisa de arquivos resolvidos e imutáveis.
        var versions = await modpacks.ListVersionsAsync(modpackId, ct);
        var latestReady = versions.FirstOrDefault(v => v.State is ModpackVersionState.Ready);
        if (latestReady is null)
            return Result<Guid>.Fail("Publique uma versão do modpack antes de criar um servidor.");

        var server = new GameServer
        {
            OwnerId = userScope.OwnerId,
            Name = name.Trim(),
            ModpackId = modpackId,
            ModpackVersionId = latestReady.Id,
            ConnectAddress = connectAddress.Trim(),
            MemoryMb = memoryMb,
            MaxPlayers = maxPlayers,
            // Segredo RCON gerado aqui, no server. Nunca exibido nem logado.
            RconSecret = RandomNumberGenerator.GetHexString(48)
        };

        await servers.AddAsync(server, ct);
        return Result<Guid>.Success(server.Id);
    }
}