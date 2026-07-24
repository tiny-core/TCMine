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
        services.AddDbContext<TcMineDbContext>(builder =>
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