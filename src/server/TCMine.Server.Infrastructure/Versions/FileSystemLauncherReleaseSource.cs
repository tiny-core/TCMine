using Microsoft.Extensions.Configuration;
using TCMine.Contracts;
using TCMine.Server.Application.Public;
using TCMine.Server.Infrastructure.Launcher;

namespace TCMine.Server.Infrastructure.Versions;

/// <summary>
///     Diz à página pública qual instalador oferecer, lendo o feed do canal
///     atual na mesma pasta que LauncherUpdateEndpoints serve (ver
///     <see cref="VelopackFeed" /> para o formato — o código anterior lia o
///     arquivo errado e nunca achava o instalador).
/// </summary>
public sealed class FileSystemLauncherReleaseSource(IConfiguration configuration) : ILauncherReleaseSource
{
    /// <summary>
    ///     Mesma chave de configuração que LauncherUpdateEndpoints usa (Web,
    ///     que esta camada não pode referenciar — Infrastructure não conhece
    ///     Web). Repetida como literal de propósito, não importada de lá.
    /// </summary>
    private const string RootKey = "LauncherUpdates:RootPath";

    public Task<LauncherReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        var raiz = configuration[RootKey];
        if (string.IsNullOrWhiteSpace(raiz))
            return Task.FromResult<LauncherReleaseInfo?>(null);

        var channel = $"win-x64-p{Protocol.Current}";
        var pastaCanal = VelopackFeed.ChannelDirectory(raiz, channel);

        var version = VelopackFeed.LatestVersion(pastaCanal, channel);
        var installer = VelopackFeed.InstallerFileName(pastaCanal, channel);

        // Sem as duas coisas não há o que oferecer: publicação no meio, ou feed
        // de outro formato. Sem download, em vez de um link que dá 404.
        if (version is null || installer is null)
            return Task.FromResult<LauncherReleaseInfo?>(null);

        return Task.FromResult<LauncherReleaseInfo?>(
            new LauncherReleaseInfo(version, $"/updates/launcher/{channel}/{installer}"));
    }
}
