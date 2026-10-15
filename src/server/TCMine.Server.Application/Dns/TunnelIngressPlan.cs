using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Dns;

/// <summary>
///     A lista nova de entradas do túnel, mexendo só no que é deste TCMine.
///     Função pura, como o <see cref="DnsPlan" />: é aqui que mora o risco de
///     apagar a entrada do painel ou de um servidor de fora, e isso se testa
///     sem Cloudflare.
///     "Deste TCMine" = os nomes dos CNAMEs de túnel que levam a marca desta
///     instalação (<paramref name="managed" />). A entrada não tem campo de
///     comentário, então a marca do DNS é a que diz de quem ela é.
/// </summary>
public static class TunnelIngressPlan
{
    public const string CatchAllService = "http_status:404";

    /// <param name="desired">nome → serviço, um por servidor com túnel.</param>
    /// <param name="managed">Os nomes que este TCMine criou antes.</param>
    public static TunnelIngressResult Merge(
        IReadOnlyList<TunnelIngressRule> current,
        IReadOnlyDictionary<string, string> desired,
        IReadOnlySet<string> managed)
    {
        var result = new List<TunnelIngressRule>();
        var problems = new List<string>();
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tail = new List<TunnelIngressRule>();

        foreach (var rule in current)
        {
            // Sem nome é regra geral (a final, ou uma por caminho): fica no
            // fim, na ordem em que estava.
            if (rule.Hostname is not { Length: > 0 } host)
            {
                tail.Add(rule);
                continue;
            }

            if (desired.TryGetValue(host, out var service))
            {
                var ours = managed.Contains(host);
                var same = string.Equals(rule.Service, service, StringComparison.OrdinalIgnoreCase)
                           && rule.Path is null;

                // Do admin e diferente: não é nossa para trocar. Igual, é a
                // mesma coisa que o admin criaria — passa a ser deste TCMine.
                if (!ours && !same)
                {
                    problems.Add(
                        $"{host}: o túnel já tem uma entrada com este nome, criada fora deste TCMine "
                        + $"({rule.Service}). Apague-a no painel Zero Trust ou escolha outro nome.");
                    result.Add(rule);
                    placed.Add(host);
                    continue;
                }

                result.Add(same ? rule : new TunnelIngressRule(host, service));
                placed.Add(host);
                continue;
            }

            // Nossa e não mais pedida (servidor apagado, túnel desligado nele).
            if (managed.Contains(host))
                continue;

            result.Add(rule);
        }

        foreach (var (host, service) in desired)
        {
            if (!placed.Contains(host))
                result.Add(new TunnelIngressRule(host, service));
        }

        // A Cloudflare recusa a lista sem a regra final.
        if (!tail.Any(r => r.Path is null))
            tail.Add(new TunnelIngressRule(null, CatchAllService));

        result.AddRange(tail);

        var changed = result.Count != current.Count
                      || result.Where((r, i) => !Same(r, current[i])).Any();

        return new TunnelIngressResult(result, changed, problems);
    }

    private static bool Same(TunnelIngressRule a, TunnelIngressRule b) =>
        string.Equals(a.Hostname, b.Hostname, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Service, b.Service, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Path, b.Path, StringComparison.Ordinal);
}

/// <param name="Changed">Falso = a lista já está como deve; nada a gravar.</param>
public sealed record TunnelIngressResult(
    IReadOnlyList<TunnelIngressRule> Rules,
    bool Changed,
    IReadOnlyList<string> Problems);
