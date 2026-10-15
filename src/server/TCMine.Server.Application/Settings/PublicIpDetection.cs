using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     A detecção do IP público obedecendo ao interruptor da instalação. Um
///     lugar só: o endereço dos servidores e a sincronização do DNS perguntam
///     o IP, e um deles esquecendo a regra voltaria a consultar serviços de
///     fora que o admin pediu para não consultar.
/// </summary>
public static class PublicIpDetection
{
    /// <returns>O IP, ou nulo quando desligada ou quando não se detectou.</returns>
    public static Task<string?> DetectAsync(
        this IPublicAddressProvider detector, InstallationSettings config, CancellationToken ct) =>
        config.PublicIpDetectionEnabled ? detector.GetAsync(ct) : Task.FromResult<string?>(null);
}
