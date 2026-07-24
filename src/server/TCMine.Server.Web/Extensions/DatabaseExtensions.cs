using Microsoft.EntityFrameworkCore;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Infrastructure.Postgres;
using TCMine.Server.Infrastructure.Sqlite;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    ///     Escolhe o provider em runtime, pela configuração.
    ///     Cada provider precisa apontar para o próprio assembly de migrations —
    ///     é a contrapartida do split que fizemos: o EF não descobre sozinho
    ///     onde o histórico está.
    /// </summary>
    public static IServiceCollection AddTcMineDatabase(
        this IServiceCollection services,
        DatabaseOptions options)
    {
        // AddDbContextFactory em vez de AddDbContext: no Blazor Server o scope
        // dura a conexão inteira, então um DbContext scoped acumularia
        // entidades de todas as telas e mais cedo ou mais tarde colidiria.
        // A factory cria um contexto curto por operação — o padrão correto
        // para Blazor Server.
        services.AddDbContextFactory<TcMineDbContext>(builder =>
        {
            switch (options.Provider)
            {
                case "Postgres":
                    builder.UseNpgsql(options.ConnectionString, npgsql =>
                    {
                        npgsql.MigrationsAssembly(
                            typeof(PostgresDbContextFactory).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__migrations_history");
                    });
                    break;

                case "Sqlite":
                    builder.UseSqlite(options.ConnectionString, sqlite =>
                    {
                        sqlite.MigrationsAssembly(
                            typeof(SqliteDbContextFactory).Assembly.FullName);
                        sqlite.MigrationsHistoryTable("__migrations_history");
                    });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Provider de banco desconhecido: '{options.Provider}'. Use 'Postgres' ou 'Sqlite'.");
            }
        });

        return services;
    }
}