using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     O que a instalação sabe sobre como os jogadores a alcançam: o host
///     público (gravado ou detectado) e o domínio dos subdomínios.
///     Num lugar só porque as precedências são regra — o gravado vence o
///     detectado, e o subdomínio só vale com o DNS configurado — e elas são
///     consultadas por quatro bordas: o hub que entrega os servidores ao
///     launcher, as duas listas de servidores do painel e o formulário. Cada uma
///     decidindo sozinha, uma delas acabaria mostrando um endereço que o
///     launcher não recebe.
/// </summary>
public sealed class GetAddressSettings(ISettingsRepository settings, IPublicAddressProvider detector)
{
    /// <param name="alwaysDetect">
    ///     Detectar mesmo havendo host gravado. Só a tela de configurações pede:
    ///     ela mostra os dois lado a lado. Para todo o resto, um host gravado
    ///     dispensa a ida à rede.
    /// </param>
    public async Task<AddressSettings> HandleAsync(bool alwaysDetect, CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);
        var configured = string.IsNullOrWhiteSpace(config.PublicHost) ? null : config.PublicHost.Trim();

        var detected = configured is null || alwaysDetect
            ? await detector.GetAsync(ct)
            : null;

        return new AddressSettings(
            configured,
            detected,
            config.DnsEnabled ? GameDns.NormalizeLabel(config.DnsBaseDomain) : null);
    }
}

/// <param name="ConfiguredHost">O host público gravado nas configurações. Nulo = nada.</param>
/// <param name="DetectedHost">O IP detectado. Nulo = não se detectou, ou não foi preciso.</param>
/// <param name="DnsBaseDomain">
///     O domínio dos subdomínios, só quando o DNS está configurado por inteiro.
///     Nulo = subdomínios não são publicados.
/// </param>
public sealed record AddressSettings(string? ConfiguredHost, string? DetectedHost, string? DnsBaseDomain)
{
    /// <summary>O host público que vale: o gravado e, na falta dele, o detectado.</summary>
    public string? PublicHost => ConfiguredHost ?? DetectedHost;

    /// <summary>O endereço publicado de um servidor. Vazio quando não há.</summary>
    public string For(GameServer server) => For(server.ConnectAddress, server.GamePort, server.Subdomain);

    /// <summary>O mesmo cálculo, para campos ainda não gravados (a prévia do formulário).</summary>
    public string For(string? connectAddress, int gamePort, string? subdomain) =>
        GameAddress.Resolve(connectAddress, gamePort, PublicHost, GameDns.ServerHost(subdomain, DnsBaseDomain));
}
