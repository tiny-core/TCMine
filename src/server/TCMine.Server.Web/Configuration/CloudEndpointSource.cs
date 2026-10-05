using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     URL da API da nuvem para os servidores de jogo: <c>Server:CloudUrl</c> se
///     definida, senão <c>Server:PublicUrl</c>. Lida a cada uso (IOptionsMonitor):
///     corrigir a configuração vale no próximo start de servidor, sem reiniciar
///     o TCMine.
/// </summary>
public sealed class CloudEndpointSource(IOptionsMonitor<ServerOptions> options) : ICloudEndpointSource
{
    public Uri? ServerFacingUrl => options.CurrentValue.CloudUrl ?? options.CurrentValue.PublicUrl;
}
