using Npgsql;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Monta a connection string a partir de campos separados.
///     Existe por dois motivos, e o segundo é o que pesa. O primeiro é conforto:
///     em painéis de NAS, colar uma linha com senha no meio é desconfortável e
///     fácil de errar. O segundo é correção: senha com <c>;</c> ou <c>=</c>
///     quebra uma connection string concatenada à mão, e o sintoma é
///     "autenticação falhou" — que manda o admin conferir a senha, que está
///     certa. Montando pelo builder do Npgsql, o valor é escapado como deve.
/// </summary>
public static class DatabaseConnection
{
    /// <summary>
    ///     Devolve a connection string derivada, ou nulo quando não há o que
    ///     derivar — porque já existe uma declarada, ou porque faltam os campos.
    /// </summary>
    public static string? Build(IConfiguration configuration, string? storageRoot)
    {
        // Declarada explicitamente ganha sempre: quem cola uma connection
        // string completa quer exatamente aquilo, incluindo parâmetros que os
        // campos separados não cobrem (SSL, timeout, pool).
        if (!string.IsNullOrWhiteSpace(configuration["Database:ConnectionString"]))
            return null;

        return configuration["Database:Provider"] switch
        {
            "Postgres" => Postgres(configuration),

            // Sqlite só depende do caminho, e ele já vem da raiz de
            // armazenamento. Note que isto é decidido PELO PROVIDER: derivar um
            // "Data Source=..." para uma instalação Postgres produziria uma
            // connection string que o Npgsql recusa, com um erro que não diz que
            // o problema é a configuração errada.
            "Sqlite" or null or "" => Sqlite(storageRoot),

            _ => null
        };
    }

    private static string? Sqlite(string? storageRoot) =>
        string.IsNullOrWhiteSpace(storageRoot)
            ? null
            : $"Data Source={storageRoot.TrimEnd('/', '\\')}/data/tcmine.db";

    private static string? Postgres(IConfiguration configuration)
    {
        // O host é o único sem padrão razoável: sem ele não há a quem conectar,
        // e inventar "localhost" mandaria a aplicação bater no próprio container.
        if (configuration["Database:Host"] is not { Length: > 0 } host)
            return null;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = configuration.GetValue("Database:Port", 5432),
            Database = Ou(configuration["Database:Name"], "tcmine"),
            Username = Ou(configuration["Database:Username"], "tcmine"),
            Password = configuration["Database:Password"]
        };

        return builder.ConnectionString;
    }

    private static string Ou(string? valor, string padrao) =>
        string.IsNullOrWhiteSpace(valor) ? padrao : valor;
}
