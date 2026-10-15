using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Domain.Settings;

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

        var detected = AddressSettings.HostFor(config) is null || alwaysDetect
            ? await detector.DetectAsync(config, ct)
            : null;

        return AddressSettings.From(config, detected);
    }
}

/// <param name="ConfiguredHost">
///     O host fixo do modo de rede (ver <see cref="AddressSettings.HostFor" />). Nulo = nenhum.
/// </param>
/// <param name="DetectedHost">O IP detectado. Nulo = não se detectou, ou não foi preciso.</param>
/// <param name="DnsBaseDomain">
///     O domínio dos subdomínios, só quando o DNS está configurado por inteiro.
///     Nulo = subdomínios não são publicados.
/// </param>
/// <param name="LanHost">O IP da máquina na rede de casa. Nulo = não informado.</param>
/// <param name="TunnelBaseDomain">O domínio dos nomes de túnel, só com o túnel configurado. Nulo = sem túnel.</param>
public sealed record AddressSettings(
    string? ConfiguredHost,
    string? DetectedHost,
    string? DnsBaseDomain,
    string? LanHost = null,
    string? TunnelBaseDomain = null)
{
    /// <summary>O nome do túnel deste servidor ("atm10-tunel.exemplo.com"). Nulo sem túnel.</summary>
    public string? TunnelFor(GameServer server) => GameDns.ServerHost(server.TunnelLabel, TunnelBaseDomain);

    /// <summary>
    ///     Onde o launcher SONDA se o caminho direto responde, antes de cair no
    ///     túnel. Não é o endereço publicado: um subdomínio é um registro SRV, e
    ///     uma sonda TCP não sabe ler SRV — bateria no nome sem porta e
    ///     concluiria que nada responde. Aqui vai sempre um host com a porta de
    ///     verdade: o escrito pelo admin, ou o host da instalação (o registro A
    ///     que o SRV também usa) mais a porta do servidor.
    /// </summary>
    public string? ProbeFor(GameServer server)
    {
        if (GameAddress.KindOf(server.ConnectAddress, server.Subdomain) is ServerAddressKind.Manual)
        {
            return GameAddress.ExplicitPort(server.ConnectAddress) is null
                ? GameAddress.WithPort(server.ConnectAddress, server.GamePort)
                : server.ConnectAddress.Trim();
        }

        return PublicHost is { } host ? GameAddress.WithPort(host, server.GamePort) : null;
    }

    /// <summary>
    ///     O host fixo que o modo de rede publica: o do admin, ou o registro A
    ///     que o TCMine mantém na Cloudflare. Nulo = vale o IP detectado.
    ///     No modo Cloudflare, um servidor sem subdomínio sai como
    ///     "mc.exemplo.com:25570" e não como o IP: o nome continua certo quando o
    ///     IP de casa muda, e o IP não.
    /// </summary>
    public static string? HostFor(InstallationSettings config) =>
        config.EffectivePublicHost ?? config.ManagedDnsHost;

    /// <summary>
    ///     Monta a partir da configuração. Público porque a aba Rede monta a
    ///     prévia "o que os jogadores recebem" com os campos AINDA NÃO salvos —
    ///     pela mesma regra, para a prévia não mentir.
    /// </summary>
    public static AddressSettings From(InstallationSettings config, string? detected) => new(
        HostFor(config),
        detected,
        config.DnsEnabled ? GameDns.NormalizeLabel(config.DnsBaseDomain) : null,
        string.IsNullOrWhiteSpace(config.LanHost) ? null : config.LanHost.Trim(),
        config.TunnelEnabled ? GameDns.NormalizeLabel(config.DnsBaseDomain) : null);

    /// <summary>O host público que vale: o gravado e, na falta dele, o detectado.</summary>
    public string? PublicHost => ConfiguredHost ?? DetectedHost;

    /// <summary>O endereço publicado de um servidor. Vazio quando não há.</summary>
    public string For(GameServer server) => For(server.ConnectAddress, server.GamePort, server.Subdomain);

    /// <summary>O mesmo cálculo, para campos ainda não gravados (a prévia do formulário).</summary>
    public string For(string? connectAddress, int gamePort, string? subdomain) =>
        GameAddress.Resolve(connectAddress, gamePort, PublicHost, GameDns.ServerHost(subdomain, DnsBaseDomain));

    /// <summary>
    ///     O endereço do servidor na rede de casa: o host local mais a porta do
    ///     servidor — a porta que o container publica nesta máquina, e não a de
    ///     um endereço escrito, que pode ser a de fora (o roteador redireciona).
    ///     Nulo sem host local.
    /// </summary>
    public string? LocalFor(GameServer server) =>
        LanHost is null ? null : GameAddress.WithPort(LanHost, server.GamePort);
}
