using Microsoft.Extensions.Configuration;
using Npgsql;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     A connection string montada a partir de campos separados.
///     O teste que mais importa aqui é o da senha com caractere especial:
///     concatenar à mão produz uma string que o driver interpreta errado, e o
///     erro que chega é "autenticação falhou" — que manda o admin conferir uma
///     senha que está certa.
/// </summary>
public sealed class DatabaseConnectionTests
{
    [Fact]
    public void Campos_separados_viram_connection_string_do_postgres()
    {
        var result = Construir(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["Database:Host"] = "postgres",
            ["Database:Port"] = "5433",
            ["Database:Name"] = "tcmine",
            ["Database:Username"] = "jocian",
            ["Database:Password"] = "segredo"
        });

        var lida = new NpgsqlConnectionStringBuilder(result);
        lida.Host.ShouldBe("postgres");
        lida.Port.ShouldBe(5433);
        lida.Database.ShouldBe("tcmine");
        lida.Username.ShouldBe("jocian");
        lida.Password.ShouldBe("segredo");
    }

    [Theory]
    [InlineData("com;ponto=virgula")]
    [InlineData("com'aspas'simples")]
    [InlineData("com\"aspas\"duplas")]
    [InlineData("com espaço no meio")]
    public void Senha_com_caractere_especial_sobrevive(string senha)
    {
        // O motivo de existir o builder em vez de interpolar texto: um ";" na
        // senha encerraria o campo e o resto viraria outro parâmetro.
        var result = Construir(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres", ["Database:Host"] = "postgres", ["Database:Password"] = senha
        });

        new NpgsqlConnectionStringBuilder(result).Password.ShouldBe(senha);
    }

    [Fact]
    public void Connection_string_declarada_ganha_dos_campos()
    {
        // Quem cola uma connection string inteira quer exatamente aquilo,
        // inclusive parâmetros que os campos não cobrem (SSL, timeout, pool).
        Construir(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["Database:ConnectionString"] = "Host=outro;Database=x",
            ["Database:Host"] = "postgres"
        }).ShouldBeNull();
    }

    [Fact]
    public void Postgres_sem_host_nao_inventa_nada()
    {
        // Assumir "localhost" mandaria a aplicação bater no próprio container,
        // e o erro de conexão não diria que faltou configurar o host.
        Construir(new Dictionary<string, string?> { ["Database:Provider"] = "Postgres" }).ShouldBeNull();
    }

    [Fact]
    public void Nome_e_usuario_tem_padrao()
    {
        var lida = new NpgsqlConnectionStringBuilder(Construir(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres", ["Database:Host"] = "postgres"
        }));

        lida.Database.ShouldBe("tcmine");
        lida.Username.ShouldBe("tcmine");
        lida.Port.ShouldBe(5432);
    }

    [Fact]
    public void Sqlite_deriva_do_caminho_da_raiz()
    {
        Construir(new Dictionary<string, string?> { ["Database:Provider"] = "Sqlite" }, "/dados/tcmine")
            .ShouldBe("Data Source=/dados/tcmine/data/tcmine.db");
    }

    [Fact]
    public void Instalacao_postgres_nao_recebe_caminho_de_sqlite()
    {
        // Regressão: a derivação pela raiz não olhava o provider e produzia um
        // "Data Source=..." mesmo para Postgres. O Npgsql recusaria com um erro
        // sobre a string, sem dizer que a configuração é que estava trocada.
        Construir(new Dictionary<string, string?> { ["Database:Provider"] = "Postgres" }, "/dados/tcmine")
            .ShouldBeNull();
    }

    private static string? Construir(Dictionary<string, string?> valores, string? raiz = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        return DatabaseConnection.Build(config, raiz);
    }
}
