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
    ICurrentUserScope userScope,
    IGamePortAllocator ports)
{
    public async Task<Result<Guid>> HandleAsync(
        Guid modpackId, string name, string connectAddress, int memoryMb, int maxPlayers,
        Guid modpackVersionId, CancellationToken ct, int gamePort = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Fail("Informe o nome do servidor.");

        // O endereço pode vir vazio: é "automático", e quem o publica o monta
        // com o host público da instalação e a porta deste servidor
        // (GameAddress.Resolve). Exigi-lo aqui obrigava o admin a descobrir e
        // digitar o próprio IP a cada servidor.

        // Só versões publicadas podem rodar (arquivos resolvidos e imutáveis).
        // Somente versões publicadas E estáveis rodam. Alpha/beta ficam de fora —
        // é onde os mods ainda podem partir o servidor.
        var ready = (await modpacks.ListVersionSummariesAsync(modpackId, ct))
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

        // Zero = "escolha por mim": a primeira livre da faixa. Um número
        // explícito vem do formulário e é conferido contra os outros servidores
        // e contra o Docker antes de qualquer coisa ser gravada.
        int port;
        if (gamePort == 0)
        {
            var allocated = await ports.AllocateAsync(ct);
            if (!allocated.Succeeded)
                return Result<Guid>.Fail(allocated.Error!);
            port = allocated.Value;
        }
        else
        {
            var available = await ports.EnsureAvailableAsync(gamePort, null, ct);
            if (!available.Succeeded)
                return Result<Guid>.Fail(available.Error!);
            port = gamePort;
        }

        var server = new GameServer
        {
            OwnerId = userScope.OwnerId,
            Name = name.Trim(),
            ModpackId = modpackId,
            ModpackVersionId = pinned.Id,
            ConnectAddress = connectAddress.Trim(),
            GamePort = port,
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
                new Membership { UserId = criador, GameServerId = server.Id, Role = ServerRole.Owner },
                ct);
        }

        return Result<Guid>.Success(server.Id);
    }
}
