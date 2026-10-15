namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     As entradas (ingress) de um túnel da Cloudflare já criado.
///     A API só sabe trocar a configuração INTEIRA de uma vez, e o mesmo túnel
///     costuma servir outras coisas do admin (o próprio painel, um servidor de
///     fora do TCMine). Por isso a porta lê e grava a lista toda, e quem decide
///     o que mexe é o <see cref="Dns.TunnelIngressPlan" />, que só toca nas
///     entradas deste TCMine.
/// </summary>
public interface ICloudflareTunnel
{
    /// <exception cref="DnsProviderException">Cloudflare fora do ar, token sem permissão, túnel inexistente.</exception>
    Task<TunnelIngress> GetIngressAsync(TunnelAccount account, CancellationToken ct);

    /// <summary>
    ///     Grava a lista. As entradas lidas voltam com o JSON original
    ///     (<see cref="TunnelIngressRule.Raw" />), e assim um campo que o TCMine
    ///     não conhece numa entrada do admin não se perde.
    /// </summary>
    Task PutIngressAsync(TunnelAccount account, TunnelIngress ingress, CancellationToken ct);
}

/// <param name="ApiToken">O mesmo token da zona; precisa também de Conta → Cloudflare Tunnel → Editar.</param>
public sealed record TunnelAccount(string AccountId, string TunnelId, string ApiToken);

/// <param name="RemotelyManaged">
///     Falso quando o túnel é configurado por arquivo (config.yml) na máquina do
///     cloudflared: aí a API não manda nas entradas, e gravar nada adiantaria.
/// </param>
/// <param name="Raw">A configuração inteira como veio, para devolver o que não é ingress intacto.</param>
public sealed record TunnelIngress(IReadOnlyList<TunnelIngressRule> Rules, bool RemotelyManaged, string? Raw = null);

/// <param name="Hostname">Nulo na regra final ("o resto"), que a Cloudflare exige por último.</param>
/// <param name="Raw">A regra como veio da API; nulo nas que o TCMine acabou de montar.</param>
public sealed record TunnelIngressRule(string? Hostname, string Service, string? Path = null, string? Raw = null);
