using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Os limites de tamanho das colunas de <c>modpack_files</c>.
///     Este teste existe por um bug que só o PostgreSQL revelou: importar um
///     pack real do CurseForge morria com "value too long for type character
///     varying(512)". O SQLite NÃO aplica o tamanho declarado num varchar, então
///     tudo passava em desenvolvimento e na suíte inteira — o limite só é regra
///     de verdade em produção.
///     Por isso o que se afirma aqui é o MODELO, e não o comportamento de uma
///     inserção: ler o limite configurado funciona em qualquer provider.
/// </summary>
public sealed class ModpackFileLimitsTests
{
    [Fact]
    public void Slug_de_override_cabe_no_maior_caminho_possivel()
    {
        // A regra que faltava. O slug de um override é o caminho MAIS um
        // prefixo, então é sempre maior que o caminho. Com os dois campos no
        // mesmo limite, um caminho no tamanho máximo gerava um slug que não
        // cabia — e o erro vinha do banco, sobre uma coluna que ninguém havia
        // configurado à mão (ela herdava a convenção global de 512).
        var caminhoMaximo = new string('a', ModpackFile.MaxPathLength);
        var slug = ModpackFile.OverrideSlug(caminhoMaximo);

        slug.Length.ShouldBeLessThanOrEqualTo(ModpackFile.MaxProjectSlugLength);
    }

    [Fact]
    public void Colunas_seguem_os_limites_declarados_no_dominio()
    {
        // Trava a ligação entre a constante e a configuração do EF: se alguém
        // mudar uma e esquecer a outra, o desencontro volta a aparecer só no
        // Postgres, em produção.
        using var factory = new SqliteTestFactory();
        using var db = factory.CreateDbContext();

        var entidade = db.Model.FindEntityType(typeof(ModpackFile))!;

        entidade.FindProperty(nameof(ModpackFile.Path))!.GetMaxLength()
            .ShouldBe(ModpackFile.MaxPathLength);

        entidade.FindProperty(nameof(ModpackFile.ProjectSlug))!.GetMaxLength()
            .ShouldBe(ModpackFile.MaxProjectSlugLength);
    }

    [Fact]
    public void Nenhuma_coluna_de_texto_ficou_com_a_convencao_global()
    {
        // O ProjectSlug quebrou justamente por não ter configuração própria:
        // herdou os 512 da convenção global do contexto, que é um valor pensado
        // para texto curto, não para um caminho de arquivo com prefixo.
        using var factory = new SqliteTestFactory();
        using var db = factory.CreateDbContext();

        var entidade = db.Model.FindEntityType(typeof(ModpackFile))!;

        foreach (var nome in new[] { nameof(ModpackFile.Path), nameof(ModpackFile.ProjectSlug) })
        {
            entidade.FindProperty(nome)!.GetMaxLength()
                .ShouldNotBe(512, $"{nome} precisa de limite próprio, não o da convenção global");
        }
    }
}
