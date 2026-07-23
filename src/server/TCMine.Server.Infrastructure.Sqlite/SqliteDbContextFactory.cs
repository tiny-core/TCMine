using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Sqlite;

/// <summary>Factory de design-time para o provider SQLite.</summary>
public sealed class SqliteDbContextFactory : IDesignTimeDbContextFactory<TcMineDbContext>
{
    public TcMineDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TCMINE_DESIGN_CONNECTION")
            ?? "Data Source=tcmine-design.db";

        var options = new DbContextOptionsBuilder<TcMineDbContext>()
            .UseSqlite(connectionString, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(SqliteDbContextFactory).Assembly.FullName);
                sqlite.MigrationsHistoryTable("__migrations_history");
            })
            .Options;

        return new TcMineDbContext(options);
    }
}