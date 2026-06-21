using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCMine_Data.Providers;

// ─────────────────────────────────────────────────────────────────────────────
//  Factories de design-time — usados SÓ pelas ferramentas 'dotnet ef' para
//  instanciar o contexto sem arrancar a aplicação web. A connection string aqui
//  não precisa ser acessível: para 'migrations add' só importa o provider, que
//  determina os tipos de coluna gerados. Em runtime quem cria o contexto é o DI
//  (ver DatabaseServiceCollectionExtensions), não estes factories.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Factory de design-time para o contexto SQLite.</summary>
public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=tcmine.db")
            .Options;
        return new SqliteAppDbContext(options);
    }
}

/// <summary>Factory de design-time para o contexto PostgreSQL.</summary>
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostgresAppDbContext>()
            .UseNpgsql("Host=localhost;Database=tcmine;Username=postgres")
            .Options;
        return new PostgresAppDbContext(options);
    }
}