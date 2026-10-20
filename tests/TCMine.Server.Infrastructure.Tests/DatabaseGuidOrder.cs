namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A ordem em que os bancos comparam GUIDs: bytes big-endian (o uuid do
///     PostgreSQL e o texto do GUID no SQLite concordam nela). NÃO é a do
///     <c>Guid.CompareTo</c> do .NET, que compara os primeiros campos em outra
///     ordem de bytes — um teste de paginação que ordenasse com ele esperaria
///     a ordem errada. Usar com <c>StringComparer.Ordinal</c>.
/// </summary>
internal static class DatabaseGuidOrder
{
    public static string Key(Guid id) => Convert.ToHexString(id.ToByteArray(bigEndian: true));
}
