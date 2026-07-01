using Microsoft.Extensions.Configuration;

namespace TCMine_Server.Infrastructure.FileSystem;

/// <summary>
/// Escolhe a <see cref="ILinkStrategy"/> certa para o ambiente, uma única vez na composição do DI.
///
/// Regra: por padrão symlink no Linux (produção/Docker) e cópia/hardlink no Windows (dev). Pode ser
/// forçada pela config <c>ServerInstances:LinkStrategy</c> = <c>"Symlink"</c> | <c>"Copy"</c> — útil,
/// por exemplo, para testar symlinks no Windows com Developer Mode ligado.
/// </summary>
public static class LinkStrategyFactory
{
    public static ILinkStrategy Create(IConfiguration config)
    {
        var configured = config["ServerInstances:LinkStrategy"];

        return configured?.Trim().ToLowerInvariant() switch
        {
            "symlink" => new SymlinkStrategy(),
            "copy" => new CopyLinkStrategy(),
            // Sem override explícito: decide pelo SO (Linux symlinka; Windows copia/hardlinka)
            _ => OperatingSystem.IsWindows() ? new CopyLinkStrategy() : new SymlinkStrategy()
        };
    }
}
