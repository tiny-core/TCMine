using TCMine.Server.Domain.Common;

namespace TCMine.Server.Application.Tests.Domain;

/// <summary>
///     O filtro de período da auditoria compara o Id com o menor GUID v7 de um
///     instante. A comparação que importa é a do BANCO: bytes em ordem
///     big-endian (uuid do PostgreSQL) e o texto em maiúsculas (como o SQLite
///     guarda o GUID). As duas são conferidas aqui; Guid.CompareTo do .NET não
///     segue essa ordem e não serve de régua.
/// </summary>
public sealed class IdTimeTests
{
    [Fact]
    public void Piso_do_instante_fica_antes_de_todo_id_daquele_ms_e_depois_dos_anteriores()
    {
        var instante = DateTimeOffset.FromUnixTimeMilliseconds(1_760_000_000_123);
        var piso = IdTime.Floor(instante);

        for (var i = 0; i < 200; i++)
        {
            var mesmoMs = Guid.CreateVersion7(instante);
            var msAnterior = Guid.CreateVersion7(instante.AddMilliseconds(-1));

            Bytes(piso, mesmoMs).ShouldBeLessThanOrEqualTo(0);
            Bytes(msAnterior, piso).ShouldBeLessThan(0);
            Texto(piso, mesmoMs).ShouldBeLessThanOrEqualTo(0);
            Texto(msAnterior, piso).ShouldBeLessThan(0);
        }
    }

    [Fact]
    public void Piso_e_um_guid_v7_valido()
    {
        var piso = IdTime.Floor(DateTimeOffset.UtcNow);

        piso.Version.ShouldBe(7);
        piso.Variant.ShouldBe(0x8, "variante RFC 9562 (bits 10)");
    }

    private static int Bytes(Guid a, Guid b) =>
        a.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(b.ToByteArray(bigEndian: true));

    private static int Texto(Guid a, Guid b) =>
        string.CompareOrdinal(a.ToString().ToUpperInvariant(), b.ToString().ToUpperInvariant());
}
