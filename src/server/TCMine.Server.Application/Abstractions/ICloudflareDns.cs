namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Os registros de DNS de uma zona na Cloudflare — só o que a sincronização
///     dos servidores de jogo precisa: listar, criar, substituir e apagar.
///     Zona e token vêm em cada chamada, e não da configuração do cliente, pelo
///     mesmo motivo da chave do CurseForge: moram na configuração da instalação,
///     e trocá-los pelo painel tem de valer na chamada seguinte.
///     Toda falha — rede, token recusado, registro inválido — sai como
///     <see cref="DnsProviderException" /> com a mensagem da própria Cloudflare.
/// </summary>
public interface ICloudflareDns
{
    /// <summary>Os registros cujo comentário começa por <paramref name="commentPrefix" />.</summary>
    Task<IReadOnlyList<DnsRecord>> ListByCommentPrefixAsync(
        DnsZone zone, string commentPrefix, CancellationToken ct);

    /// <summary>Os registros com exatamente este nome, de qualquer tipo e de quem for.</summary>
    Task<IReadOnlyList<DnsRecord>> ListByNameAsync(DnsZone zone, string name, CancellationToken ct);

    Task CreateAsync(DnsZone zone, DnsRecordSpec record, CancellationToken ct);

    /// <summary>Substitui o registro inteiro pelo que está em <paramref name="record" />.</summary>
    Task ReplaceAsync(DnsZone zone, string recordId, DnsRecordSpec record, CancellationToken ct);

    Task DeleteAsync(DnsZone zone, string recordId, CancellationToken ct);
}

/// <summary>Onde e com que credencial. O token nunca vai para log nem para mensagem de erro.</summary>
public sealed record DnsZone(string ZoneId, string ApiToken);

/// <summary>
///     Um registro como a sincronização o enxerga. Cobre os dois tipos que ela
///     mantém: num A, <see cref="Target" /> é o IP e <see cref="Port" /> é zero;
///     num SRV, <see cref="Target" /> é o nome para onde ele aponta.
/// </summary>
public sealed record DnsRecordSpec(string Type, string Name, string Target, int Port, string Comment);

public sealed record DnsRecord(string Id, DnsRecordSpec Spec);

public static class DnsRecordTypes
{
    public const string A = "A";
    public const string Srv = "SRV";
}

public sealed class DnsProviderException : Exception
{
    public DnsProviderException()
    {
    }

    public DnsProviderException(string message) : base(message)
    {
    }

    public DnsProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
