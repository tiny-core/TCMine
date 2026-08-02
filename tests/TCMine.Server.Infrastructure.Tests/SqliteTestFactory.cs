using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Factory de DbContext para testes, sobre SQLite in-memory. Mantém UMA conexão
///     viva (o banco in-memory morre quando a última conexão fecha) e serve todos os
///     contextos sobre ela — assim o que um grava, outro lê, como num banco real.
/// </summary>
public sealed class SqliteTestFactory : IDbContextFactory<TcMineDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TcMineDbContext> _options;

    public SqliteTestFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // mantém o banco vivo enquanto a factory existir

        _options = new DbContextOptionsBuilder<TcMineDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Cria o schema a partir do modelo (sem migrations — mais rápido no teste).
        using var db = new TcMineDbContext(_options);
        db.Database.EnsureCreated();
    }

    public TcMineDbContext CreateDbContext() => new(_options);

    public Task<TcMineDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose() => _connection.Dispose();
}
