namespace TCMine_Application.Launcher;

/// <summary>Resultado do ping a um servidor de Minecraft.</summary>
public sealed record ServerPing(bool Online, int PlayersOnline, int PlayersMax, string Motd)
{
    public static ServerPing Offline => new(false, 0, 0, string.Empty);
}

/// <summary>Porta do Server List Ping (estado online/jogadores de um servidor do modpack).</summary>
public interface IServerPinger
{
    Task<ServerPing> PingAsync(string host, int port, int timeoutMs = 4000);
}
