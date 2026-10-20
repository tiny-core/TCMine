using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

public sealed class UpdateGameServer(
    IServerRepository servers,
    IServerWhitelistSync whitelist,
    ICurrentUserScope scope,
    IGamePortAllocator ports,
    IDnsSyncSignal dnsSync)
{
    public const string ConcurrentEditMessage =
        "Outra pessoa salvou este servidor enquanto o formulário estava aberto. "
        + "Feche e abra de novo para ver o que mudou; nada do que você editou foi gravado.";

    public async Task<Result> HandleAsync(
        Guid id, string name, string connectAddress, int memoryMb, int maxPlayers,
        bool whitelistEnabled, CancellationToken ct, int gamePort = 0, string? subdomain = null,
        IReadOnlyDictionary<string, string>? options = null, string? tunnelLabel = null,
        long? expectedRowVersion = null)
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

        // O formulário manda o RowVersion do servidor que ele mostrou. Diferente
        // do banco = alguém salvou o formulário depois disso, e gravar agora
        // apagaria o que essa pessoa salvou. Nulo = quem chama não tem a versão
        // que o usuário viu (sem formulário no meio): vale só a conferência do
        // banco, logo abaixo, contra a leitura daqui.
        if (expectedRowVersion is { } seen && seen != server.RowVersion)
            return Result.Fail(ConcurrentEditMessage);

        // Nulo = não mexe nas opções de jogo; um dicionário (mesmo vazio)
        // substitui todas. Como as demais conferências, antes de alterar algo.
        var (gameOptions, optionsError) = GameOptions.Normalize(options);
        if (options is not null && optionsError is not null)
            return Result.Fail(optionsError);

        // Nulo = não mexe no subdomínio; vazio = tira. Como a porta abaixo, a
        // recusa vem antes de qualquer campo ser alterado.
        var label = GameDns.NormalizeLabel(subdomain);

        // Uma forma só (ver CreateGameServer). Só confere o que VEIO: um
        // subdomínio nulo é "não mexe", e servidores de antes da escolha única
        // com os dois continuam a salvar até alguém escolher no formulário.
        if (label is not null && !string.IsNullOrWhiteSpace(connectAddress))
            return Result.Fail(GameAddress.BothAddressesMessage);

        if (label is not null && label != server.Subdomain)
        {
            if (!GameDns.IsValidLabel(label))
                return Result.Fail(GameDns.InvalidLabelMessage);

            if (GameDns.OwnerOf(label, await servers.ListAllAsync(ct), id) is { } taken)
                return Result.Fail($"O subdomínio \"{label}\" já é usado pelo servidor \"{taken.Name}\".");
        }

        // Nulo = não mexe no túnel; vazio = tira. Mesma regra do subdomínio.
        var tunnel = GameDns.NormalizeLabel(tunnelLabel);
        if (tunnel is not null && tunnel != server.TunnelLabel
            && GameDns.TunnelLabelProblem(tunnel, await servers.ListAllAsync(ct), id) is { } tunnelProblem)
            return Result.Fail(tunnelProblem);

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

        if (tunnelLabel is not null)
            server.TunnelLabel = tunnel;

        // Chegam ao jogo no próximo início: entram na impressão digital do
        // container, que é recriado com o mundo intacto.
        if (options is not null)
            server.OptionsJson = GameOptions.Serialize(gameOptions);

        server.MemoryMb = memoryMb;
        server.MaxPlayers = maxPlayers;

        var whitelistMudou = server.WhitelistEnabled != whitelistEnabled;
        server.WhitelistEnabled = whitelistEnabled;
        server.UpdatedAt = DateTimeOffset.UtcNow;

        // O banco confere de novo: entre a leitura acima e esta linha, outro
        // salvar pode ter passado. Antes de pedir DNS e whitelist, que só fazem
        // sentido se a gravação valeu.
        if (!await servers.UpdateAsync(server, ct))
            return Result.Fail(ConcurrentEditMessage);

        // Porta ou subdomínio podem ter mudado, e os dois estão no SRV. Pedir
        // sempre é mais barato do que decidir aqui o que conta como mudança.
        dnsSync.Request();

        // Só quando muda: aplicar a cada salvamento reescreveria a lista inteira
        // por causa de um ajuste de RAM.
        if (whitelistMudou)
            await whitelist.HandleAsync(id, ct);

        return Result.Success();
    }
}
