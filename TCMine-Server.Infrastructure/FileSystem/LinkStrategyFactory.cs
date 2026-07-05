using Microsoft.Extensions.Configuration;

namespace TCMine_Server.Infrastructure.FileSystem;

/// <summary>
///     Escolhe a <see cref="ILinkStrategy" /> certa para o ambiente, uma única vez na composição do DI.
///     Padrão: <see cref="CopyLinkStrategy" /> (hardlink) em <b>todos os lugares</b> — funciona no Docker-out-of-Docker
///     (o symlink quebraria, pois aponta para um caminho do container do servidor que a instância não enxerga).
///     Pode ser forçada pela config <c>ServerInstances:LinkStrategy</c> = <c>"Symlink"</c> | <c>"Copy"</c> —
///     <c>Symlink</c> só serve quando o TCMine-Server roda direto no host (sem DooD).
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
            // Sem override: hardlink (DooD-safe, custo de disco ~zero). Symlink é opt-in explícito.
            _ => new CopyLinkStrategy()
        };
    }
}