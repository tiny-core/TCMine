namespace TCMine.Server.Domain.Common;

/// <summary>
///     Converte um instante no menor GUID v7 daquele milissegundo.
///     Serve para filtrar por período pelo <c>Id</c> em vez do <c>CreatedAt</c>:
///     o SQLite não compara nem ordena <c>DateTimeOffset</c>, e o <c>Id</c> já
///     está nos índices. "Criado a partir de T" vira <c>Id &gt;= Floor(T)</c>;
///     "antes de T" vira <c>Id &lt; Floor(T)</c>.
///     Vale porque o GUID v7 começa pelos 48 bits do instante (em ms, big-endian):
///     tanto o uuid do PostgreSQL quanto o texto do GUID no SQLite comparam
///     nessa ordem. A comparação do próprio .NET (<c>Guid.CompareTo</c>) NÃO
///     segue essa ordem — use o resultado só em consulta ao banco.
/// </summary>
public static class IdTime
{
    public static Guid Floor(DateTimeOffset instant)
    {
        var ms = Math.Max(0, instant.ToUnixTimeMilliseconds());
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 5; i >= 0; i--)
        {
            bytes[i] = (byte)(ms & 0xFF);
            ms >>= 8;
        }

        bytes[6] = 0x70; // versão 7, resto do campo zerado
        bytes[8] = 0x80; // variante RFC 9562 (bits 10), resto zerado
        return new Guid(bytes, bigEndian: true);
    }
}
