using System.Security.Cryptography;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

public sealed class CreateGameServer(
    IServerRepository servers,
    IModpackRepository modpacks,
    IMembershipRepository memberships,
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
        // Somente versões publicadas E estáveis rodam. Alpha/beta ficam de fora —
        // é onde os mods ainda podem partir o servidor.
        var ready = (await modpacks.ListVersionsAsync(modpackId, ct))
            .Where(v => v.State is ModpackVersionState.Ready && !v.IsPreRelease)
            .ToList();
        if (ready.Count == 0)
            return Result<Guid>.Fail("Publique uma versão estável (não-alpha) antes de criar um servidor.");

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

        // Quem cria vira Owner do servidor. Sem este vínculo o servidor nasceria
        // sem ninguém que possa convidar ou apagá-lo: o OwnerId sozinho é
        // costura de multi-tenant, não papel — quem decide permissão é o
        // Membership, e ele precisa existir desde o primeiro instante.
        if (userScope.UserId is { } criador)
        {
            await memberships.AddAsync(
                new Membership
                {
                    UserId = criador,
                    GameServerId = server.Id,
                    Role = ServerRole.Owner
                },
                ct);
        }

        return Result<Guid>.Success(server.Id);
    }
}
