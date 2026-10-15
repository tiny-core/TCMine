using System.Security.Cryptography;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

public sealed class CreateGameServer(
    IServerRepository servers,
    IModpackRepository modpacks,
    IMembershipRepository memberships,
    ICurrentUserScope userScope,
    IGamePortAllocator ports,
    IDnsSyncSignal dnsSync)
{
    public async Task<Result<Guid>> HandleAsync(
        Guid modpackId, string name, string connectAddress, int memoryMb, int maxPlayers,
        Guid modpackVersionId, CancellationToken ct, int gamePort = 0, string? subdomain = null,
        IReadOnlyDictionary<string, string>? options = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Fail("Informe o nome do servidor.");

        // Subir um servidor com um modpack é usá-lo por inteiro: os arquivos
        // dele vão para a máquina e para quem entrar. Sem esta conferência,
        // qualquer conta criava servidor com o modpack de outra — bastava o
        // id. Editor para cima; a mensagem não distingue "não existe" de "não
        // é seu" (ver ModpackAuthorization).
        var auth = await userScope.RequireAsync(modpackId, ModpackAccessPolicy.CanEdit, ct);
        if (!auth.Succeeded)
            return Result<Guid>.Fail(auth.Error!);

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

        // Opções de jogo: só o que está no catálogo, com valor válido.
        var (gameOptions, optionsError) = GameOptions.Normalize(options);
        if (optionsError is not null)
            return Result<Guid>.Fail(optionsError);

        // Opcional. Conferido aqui, e não só pelo índice único do banco, para o
        // erro dizer QUAL servidor já tem o nome.
        var label = GameDns.NormalizeLabel(subdomain);

        // Uma forma só: com os dois, o escrito venceria em silêncio e o
        // subdomínio viraria um registro na Cloudflare que ninguém usa.
        if (label is not null && !string.IsNullOrWhiteSpace(connectAddress))
            return Result<Guid>.Fail(GameAddress.BothAddressesMessage);

        if (label is not null)
        {
            if (!GameDns.IsValidLabel(label))
                return Result<Guid>.Fail(GameDns.InvalidLabelMessage);

            if (GameDns.OwnerOf(label, await servers.ListAllAsync(ct), null) is { } taken)
                return Result<Guid>.Fail($"O subdomínio \"{label}\" já é usado pelo servidor \"{taken.Name}\".");
        }

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
            Subdomain = label,
            OptionsJson = GameOptions.Serialize(gameOptions),
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

        // Servidor novo com subdomínio precisa do SRV dele.
        dnsSync.Request();

        return Result<Guid>.Success(server.Id);
    }
}
