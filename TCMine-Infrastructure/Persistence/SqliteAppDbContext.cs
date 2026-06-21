using Microsoft.EntityFrameworkCore;

namespace TCMine_Infrastructure.Persistence;

/// <summary>
/// Contexto concreto para SQLite. As migrations deste provider ficam em
/// <c>Data/Migrations/Sqlite</c> (geradas com <c>--context SqliteAppDbContext</c>).
/// </summary>
public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);