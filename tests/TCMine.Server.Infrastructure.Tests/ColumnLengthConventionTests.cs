using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A convenção global dá 512 caracteres a TODA coluna de texto. Isso é bom
///     por padrão e errado para as poucas colunas que crescem com o dado.
///     Estes testes existem porque desfazer a convenção não é óbvio: chamar
///     <c>builder.Property(x => x.Campo)</c> sem configurar nada NÃO remove o
///     limite herdado. O código dizia "sem limite" num comentário e a coluna
///     saía <c>varchar(512)</c> assim mesmo — e só o PostgreSQL reclamava.
/// </summary>
public sealed class ColumnLengthConventionTests
{
    [Fact]
    public void Snapshot_da_origem_nao_tem_limite()
    {
        // O snapshot guarda um par projeto/arquivo e o nome de cada mod do pack.
        // Não existe número certo aqui: ele cresce com o pack, então o limite
        // tem de ser ausente, não grande.
        using var factory = new SqliteTestFactory();
        using var db = factory.CreateDbContext();

        db.Model.FindEntityType(typeof(ModpackVersion))!
            .FindProperty(nameof(ModpackVersion.UpstreamSnapshotJson))!
            .GetMaxLength()
            .ShouldBeNull("o snapshot cresce com o tamanho do pack; Property() pelado não desfaz a convenção");
    }

    [Fact]
    public void Colunas_de_diagnostico_cabem_uma_mensagem_de_erro()
    {
        // Detail guarda a mensagem que a origem devolveu. Uma coluna curta aqui
        // derruba a ingestão justamente ao registrar POR QUE um mod falhou: o
        // diagnóstico quebrando a operação que deveria explicar.
        using var factory = new SqliteTestFactory();
        using var db = factory.CreateDbContext();

        var pendente = db.Model.FindEntityType(typeof(PendingMod))!;

        pendente.FindProperty(nameof(PendingMod.Detail))!.GetMaxLength()!.Value
            .ShouldBeGreaterThanOrEqualTo(2048);

        pendente.FindProperty(nameof(PendingMod.PageUrl))!.GetMaxLength()!.Value
            .ShouldBeGreaterThanOrEqualTo(1024);
    }
}
