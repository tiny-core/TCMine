using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;

namespace TCMine.Contracts.Hubs;

public static class HubRoutes
{
    public const string Main = "/hubs/v1/main";
}

/// <summary>
///     Métodos que o LAUNCHER chama no server.
///     O Hub implementa esta interface. Do lado do cliente vamos escrever
///     wrappers à mão sobre ela — o source generator de proxy tipado do SignalR
///     nunca saiu de preview.
/// </summary>
public interface IServerHub
{
    Task<IReadOnlyList<ModpackDto>> GetModpacksAsync();

    Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId);

    Task<IReadOnlyList<GameServerDto>> GetServersAsync();

    /// <summary>Assina eventos de um servidor. Valida permissão no server.</summary>
    Task SubscribeServerAsync(Guid serverId);

    Task UnsubscribeServerAsync(Guid serverId);

    /// <summary>
    ///     Comando moderado. O launcher NUNCA fala RCON direto: a senha jamais sai
    ///     do server. Aqui o server autoriza, valida contra a allowlist e traduz.
    /// </summary>
    Task<CommandResultDto> SendCommandAsync(Guid serverId, string command, IReadOnlyList<string> args);
}

/// <summary>
///     Métodos que o SERVIDOR chama no launcher. Usado como Hub &lt; ILauncherClient &gt;.
///     Push é otimização, não fonte da verdade. Se a conexão cair bem na hora de
///     um publish, o evento se perde para sempre — por isso o launcher reconcilia
///     o estado no OnConnected e ao ganhar foco da janela.
/// </summary>
public interface ILauncherClient
{
    Task ModpackVersionPublished(Guid modpackId, Guid versionId);

    Task ServerStatusChanged(Guid serverId, GameServerStatus status);

    Task ServerPlayerCountChanged(Guid serverId, int online, int max);

    Task ConsoleLine(Guid serverId, ConsoleLineDto line);

    /// <summary>
    ///     O papel do usuário mudou. Sem isto, quem foi rebaixado continua
    ///     recebendo o stream do console até reconectar.
    /// </summary>
    Task RoleChanged(Guid serverId, ServerRoleDto role);
}

public sealed record ConsoleLineDto(DateTimeOffset Timestamp, string Text, ConsoleStream Stream);

public enum ConsoleStream
{
    StdOut,
    StdErr
}

public sealed record CommandResultDto(bool Accepted, string? Output, string? Error);