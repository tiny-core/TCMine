using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Postgres;

/// <summary>
///     Usada apenas pela ferramenta dotnet ef, em tempo de design.
///     Sem ela, o comando precisaria de um projeto executável para descobrir
///     como construir o DbContext — e amarraria a geração de migrations à
///     configuração da aplicação web.
///     A connection string aqui não precisa apontar para um banco real: o EF só
///     usa o provider para saber qual SQL gerar. Ela só é usada de verdade em
///     comandos que tocam o banco, como "dotnet ef database update".
/// </summary>
public sealed class PostgresDbContextFactory : IDesignTimeDbContextFactory<TcMineDbContext>
{
    public TcMineDbContext CreateDbContext(string[] args)
    {
        // Permite apontar para um banco real quando necessário:
        //   TCMINE_DESIGN_CONNECTION="..." dotnet ef database update
        var connectionString =
            Environment.GetEnvironmentVariable("TCMINE_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=tcmine;Username=tcmine;Password=tcmine";

        var options = new DbContextOptionsBuilder<TcMineDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                // Diz ao EF que as migrations vivem NESTE assembly, e não
                // junto do DbContext. É o que viabiliza o split por provider.
                npgsql.MigrationsAssembly(typeof(PostgresDbContextFactory).Assembly.FullName);

                // Tabela de histórico com nome explícito, no mesmo padrão
                // snake_case das demais.
                npgsql.MigrationsHistoryTable("__migrations_history");
            })
            .Options;

        return new TcMineDbContext(options);
    }
}