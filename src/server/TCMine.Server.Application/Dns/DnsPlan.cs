using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Dns;

/// <summary>
///     O que fazer para a zona ficar igual ao desejado — a decisão, sem a rede.
///     É o mesmo desenho declarativo do manifesto dos modpacks: descreve-se o
///     estado final e calcula-se a diferença. Assim uma sincronização que falhou
///     a meio não deixa nada para consertar à mão; a seguinte recalcula e
///     termina o serviço.
///     A identidade de um registro é o COMENTÁRIO, não o nome: é ele que diz de
///     que servidor o registro é, e é o que deixa trocar o subdomínio de um
///     servidor virar uma alteração em vez de um apaga-e-cria.
/// </summary>
public sealed record DnsPlan(
    IReadOnlyList<DnsRecord> Delete,
    IReadOnlyList<DnsRecordChange> Replace,
    IReadOnlyList<DnsRecordSpec> Create,
    int Unchanged)
{
    /// <param name="desired">O que deve existir. Um comentário por registro.</param>
    /// <param name="existing">O que a zona tem com a marca desta instalação.</param>
    /// <param name="keepComment">
    ///     Comentário de um registro que não se sabe se ainda é desejado, e que por
    ///     isso não pode ser apagado nesta rodada. É o registro A quando o IP
    ///     público não foi detectado: apagá-lo derrubaria todos os servidores por
    ///     causa de uma consulta que falhou.
    /// </param>
    public static DnsPlan Build(
        IReadOnlyList<DnsRecordSpec> desired,
        IReadOnlyList<DnsRecord> existing,
        string? keepComment)
    {
        var delete = new List<DnsRecord>();
        var replace = new List<DnsRecordChange>();
        var create = new List<DnsRecordSpec>();
        var unchanged = 0;

        var wanted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spec in desired)
        {
            wanted.Add(spec.Comment);

            var current = existing.Where(r => r.Spec.Comment == spec.Comment).ToList();

            if (current.Count == 0)
            {
                create.Add(spec);
                continue;
            }

            if (Same(current[0].Spec, spec))
                unchanged++;
            else
                replace.Add(new DnsRecordChange(current[0].Id, spec));

            // Dois registros com a mesma marca só nascem de uma corrida entre
            // duas sincronizações. Fica o primeiro; os demais são sobra.
            delete.AddRange(current.Skip(1));
        }

        delete.AddRange(existing.Where(r => !wanted.Contains(r.Spec.Comment) && r.Spec.Comment != keepComment));

        return new DnsPlan(delete, replace, create, unchanged);
    }

    // Nomes de DNS não distinguem maiúsculas, e a Cloudflare devolve-os em
    // minúsculas: comparar ao pé da letra reescreveria todos os registros a
    // cada rodada.
    private static bool Same(DnsRecordSpec a, DnsRecordSpec b) =>
        string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Target, b.Target, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port;
}

public sealed record DnsRecordChange(string RecordId, DnsRecordSpec Record);
