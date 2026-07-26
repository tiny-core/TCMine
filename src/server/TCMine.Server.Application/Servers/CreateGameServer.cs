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
        Guid modpackId, string name, string connectAddress, int memoryMb, int maxPlayers,
        Guid modpackVersionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Fail("Informe o nome do servidor.");
        if (string.IsNullOrWhiteSpace(connectAddress))
            return Result<Guid>.Fail("Informe o endereço de conexão.");

        // Só versões publicadas podem rodar (arquivos resolvidos e imutáveis).
        var ready = (await modpacks.ListVersionsAsync(modpackId, ct))
            .Where(v => v.State is ModpackVersionState.Ready)
            .ToList();
        if (ready.Count == 0)
            return Result<Guid>.Fail("Publique uma versão do modpack antes de criar um servidor.");

        // A versão vem do formulário. Guid.Empty = usa a mais recente (a lista
        // já vem do mais novo para o mais antigo).
        var pinned = modpackVersionId == Guid.Empty
            ? ready[0]
            : ready.FirstOrDefault(v => v.Id == modpackVersionId);
        if (pinned is null)
            return Result<Guid>.Fail("Selecione uma versão publicada válida.");

        var server = new GameServer
        {
            OwnerId = userScope.OwnerId,
            Name = name.Trim(),
            ModpackId = modpackId,
            ModpackVersionId = pinned.Id,
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