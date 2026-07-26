using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Traduz uma versão fixada nas variáveis de ambiente do itzg/minecraft-server.
///     O container só entende strings; aqui é onde o nosso domínio vira config dele.
/// </summary>
internal static class ItzgEnv
{
    // O itzg usa nomes próprios de "TYPE" por loader.
    public static string ToServerType(ModLoader loader)
    {
        return loader switch
        {
            ModLoader.Vanilla => "VANILLA",
            ModLoader.Forge => "FORGE",
            ModLoader.NeoForge => "NEOFORGE",
            ModLoader.Fabric => "FABRIC",
            ModLoader.Quilt => "QUILT",
            _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null)
        };
    }
}